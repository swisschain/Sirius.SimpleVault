using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SimpleVault.Common.Persistence.Cursors
{
    public class CursorRepository : ICursorRepository
    {
        private readonly DbContextOptionsBuilder<DatabaseContext> _dbContextOptionsBuilder;

        public CursorRepository(DbContextOptionsBuilder<DatabaseContext> dbContextOptionsBuilder)
        {
            _dbContextOptionsBuilder = dbContextOptionsBuilder;
        }

        public async Task UpdateOrAddAsync(Domain.Cursor cursorEntity)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);

            var entity = MapToEntity(cursorEntity);

            context.Cursor.Update(entity);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                context.Cursor.Local.Clear();
                context.Cursor.Add(entity);

                await context.SaveChangesAsync();
            }
        }

        public async Task<Domain.Cursor> GetOrDefaultAsync(string id)
        {
            await using var context = new DatabaseContext(_dbContextOptionsBuilder.Options);
            var entity = await context.Cursor.FirstOrDefaultAsync(x => x.Id == id);

            return entity == null ? null : MapToDomain(entity);
        }

        private static CursorEntity MapToEntity(Domain.Cursor cursor)
        {
            return new CursorEntity
            {
                Cursor = cursor.CursorValue,
                Id = cursor.Id
            };
        }

        private static Domain.Cursor MapToDomain(CursorEntity entity)
        {
            var wallet = Domain.Cursor.Restore(entity.Cursor, entity.Id);

            return wallet;
        }
    }
}
