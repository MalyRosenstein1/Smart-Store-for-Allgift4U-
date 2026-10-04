using SmartStore.DAL.Data;
using SmartStore.DAL.Models;

namespace SmartStore.DAL.Repositories;

public class AILogRepository
{
    private readonly SmartStoreContext _context;

    public AILogRepository(SmartStoreContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AI_Log log)
    {
        _context.AI_Logs.Add(log);
        await _context.SaveChangesAsync();
    }
}
