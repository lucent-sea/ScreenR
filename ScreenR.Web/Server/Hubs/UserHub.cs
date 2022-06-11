﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ScreenR.Web.Server.Hubs
{
    public interface IUserHubClient
    {

    }

    [Authorize]
    public class UserHub : Hub<IUserHubClient>
    {
        private readonly IHubContext<DeviceHub, IDeviceHubClient> _deviceHubContext;

        public UserHub(IHubContext<DeviceHub, IDeviceHubClient> deviceHubContext)
        {
            _deviceHubContext = deviceHubContext;
        }

        public async IAsyncEnumerable<byte[]> GetDesktopStream(Guid sessionId, string passphrase)
        {
            await _deviceHubContext.Clients
                .Groups(sessionId.ToString())
                .StartDesktopStream(passphrase);

            var result = await DeviceHub.GetStreamSession(sessionId, TimeSpan.FromSeconds(30));

            if (!result.IsSuccess || result.Value?.Stream is null)
            {
                yield break;
            }

            await foreach (var streamByte in result.Value.Stream)
            {
                yield return streamByte;
            }

            result.Value.EndSignal.Release();
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }
}
