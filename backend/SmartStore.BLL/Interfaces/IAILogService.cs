using SmartStore.DAL.Models;

namespace SmartStore.BLL.Interfaces;

public interface IAILogService
{
    Task AddLogAsync(AI_Log log);
}
