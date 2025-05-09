﻿using UZonMail.Core.SignalRHubs.Notify;
using UZonMail.DB.SQL.Core.Emails;

namespace UZonMail.Core.SignalRHubs.OutboxInfo
{
    /// <summary>
    /// 发件箱状态变更
    /// </summary>
    public interface IOutboxStatusChanged
    {
        Task OutboxStatusChanged(Outbox outbox);

        Task InboxStatusChanged(Inbox inbox);
    }
}
