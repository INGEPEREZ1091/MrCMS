using MrCMS.Entities.Notifications;
using MrCMS.Helpers;
using MrCMS.Services.Notifications;

namespace MrCMS.Events.Documents
{
    public class DocumentUpdatedNotification : IOnDocumentUpdated
    {
        private readonly INotificationPublisher _notificationPublisher;

        public DocumentUpdatedNotification(INotificationPublisher notificationPublisher)
        {
            _notificationPublisher = notificationPublisher;
        }

        public void Execute(OnDocumentUpdatedEventArgs args)
        {
            var message = string.Format("<a href=\"/Admin/{2}/Edit/{1}\">{0}</a> has been updated.", args.Document.Name,
                                        args.Document.Id, args.Document.GetAdminController());
            _notificationPublisher.PublishNotification(message, PublishType.Both, NotificationType.AdminOnly);
        }
    }
}