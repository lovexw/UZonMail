﻿using UZonMailService.Models.SQL.EmailSending;
using UZonMailService.Models.SQL.Files;

namespace UZonMailService.Controllers.Emails.Models
{
    /// <summary>
    /// 发件历史结果
    /// </summary>
    public class SendingHistoryResult : SendingGroup
    {
        public int TemplatesCount { get; }
        public int OutboxesCount { get; }
        public int InboxesCount { get; }
        public int CcBoxesCount { get; }
        public int BccBoxesCount { get; }
        public List<FileUsage>? Attachments { get; }


        public SendingHistoryResult(SendingGroup sendingGroup)
        {
            Id = sendingGroup.Id;
            UserId = sendingGroup.UserId;
            Subjects = sendingGroup.Subjects;
            TemplatesCount = sendingGroup.Templates != null ? sendingGroup.Templates.Count : 0;
            OutboxesCount = sendingGroup.Outboxes != null ? sendingGroup.Outboxes.Count : 0;
            InboxesCount = sendingGroup.Inboxes != null ? sendingGroup.Inboxes.Count : 0;
            CcBoxesCount = sendingGroup.CcBoxes != null ? sendingGroup.CcBoxes.Count : 0;
            BccBoxesCount = sendingGroup.BccBoxes != null ? sendingGroup.BccBoxes.Count : 0;
            Attachments = sendingGroup.Attachments;
            Status = sendingGroup.Status;
            SendingType = sendingGroup.SendingType;
            CreateDate = sendingGroup.CreateDate;
            SendStartDate = sendingGroup.SendStartDate;
            SendEndDate = sendingGroup.SendEndDate;
            ScheduleDate = sendingGroup.ScheduleDate;
            TotalCount = sendingGroup.TotalCount;
            SuccessCount = sendingGroup.SuccessCount;
            SentCount = sendingGroup.SentCount;
        }
    }
}
