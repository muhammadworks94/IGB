namespace IGB.Web.Zoom;

public interface IZoomClient
{
    Task<ZoomCreateMeetingResponse?> CreateMeetingAsync(ZoomCreateMeetingRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteMeetingAsync(string meetingId, CancellationToken cancellationToken = default);
}


