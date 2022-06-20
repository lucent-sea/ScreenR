﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScreenR.Desktop.Shared.Dtos;
using ScreenR.Desktop.Shared.Interfaces;
using ScreenR.Desktop.Shared.Models;

namespace ScreenR.Web.Server.Hubs
{
    //[Authorize]
    public class UserHub : Hub<IUserHubClient>
    {
        private readonly IHubContext<DesktopHub, IDesktopHubClient> _desktopHubContext;
        private readonly IHubContext<ServiceHub, IServiceHubClient> _serviceHubContext;

        public UserHub(
            IHubContext<DesktopHub, IDesktopHubClient> deviceHubContext,
            IHubContext<ServiceHub, IServiceHubClient> serviceHubContext)
        {
            _desktopHubContext = deviceHubContext;
            _serviceHubContext = serviceHubContext;
        }

        public async IAsyncEnumerable<DesktopFrameChunk> GetDesktopStream(Guid sessionId, Guid requestId, string passphrase)
        {
            var streamToken = new StreamToken(sessionId, requestId);

            await _desktopHubContext.Clients
                .Groups(sessionId.ToString())
                .StartDesktopStream(streamToken, passphrase);

            var result = await DesktopHub.GetStreamSession(streamToken, TimeSpan.FromSeconds(30));

            if (!result.IsSuccess || result.Value?.Stream is null)
            {
                yield break;
            }

            try
            {
                await foreach (var chunk in result.Value.Stream)
                {
                    yield return chunk;
                }
            }
            finally
            {
                result.Value.EndSignal.Release();
            }

        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public async Task RequestDesktopStream(Guid deviceId, Guid requestId)
        {
            await _serviceHubContext.Clients
                .Group(deviceId.ToString())
                .RequestDesktopStream(requestId, Context.ConnectionId);
        }
    }
}
