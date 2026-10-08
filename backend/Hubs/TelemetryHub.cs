using Microsoft.AspNetCore.SignalR;

namespace SmartPlanter.Api.Hubs;

public class TelemetryHub : Hub
{
    // Хаб трансляции событий: ReceiveTelemetry, ReceiveAlert, ReceiveWatering
}