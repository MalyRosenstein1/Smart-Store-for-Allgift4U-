using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Models;
using SmartStore.DAL.Repositories;

namespace SmartStore.BLL.Services;

public class AILogService : IAILogService
{
    private readonly AILogRepository _repository;

    public AILogService(AILogRepository repository)
    {
        _repository = repository;
    }

    public async Task AddLogAsync(AI_Log log) =>
        await _repository.AddAsync(log);
}
