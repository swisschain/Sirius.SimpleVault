using System.Threading.Tasks;
using SimpleVault.Common.Domain;

namespace SimpleVault.Common.Persistence
{
    public interface ICursorRepository
    {
        Task UpdateOrAddAsync(Cursor cursorEntity);

        Task<Cursor> GetOrDefaultAsync(string id);
    }
}
