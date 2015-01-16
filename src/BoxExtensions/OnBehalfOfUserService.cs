using System.Threading.Tasks;
using Box.V2;
using Box.V2.Request;
using Box.V2.Services;

namespace UITS.Box.Collaborations.BoxExtensions
{
    public class OnBehalfOfUserService : IBoxService
    {
        private readonly string _userId;
        private readonly BoxService _boxService;

        public OnBehalfOfUserService(IRequestHandler handler, string userId)
        {
            _userId = userId;
            _boxService = new BoxService(handler);
        }

        public async Task<IBoxResponse<T>> ToResponseAsync<T>(IBoxRequest request) where T : class
        {
            request.HttpHeaders.Add("As-User", _userId);
            return await _boxService.ToResponseAsync<T>(request);
        }

        public async Task<IBoxResponse<T>> EnqueueAsync<T>(IBoxRequest request) where T : class
        {
            return await _boxService.EnqueueAsync<T>(request);
        }
    }
}