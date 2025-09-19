﻿using log4net;
using UZonMail.Core.Services.SendCore.Contexts;
using UZonMail.Core.Services.SendCore.Outboxes;
using UZonMail.Core.Services.SendCore.Utils;
using UZonMail.Core.Services.SendCore.WaitList;
using UZonMail.Core.Services.Settings;
using UZonMail.Core.Services.Settings.Model;
using UZonMail.Core.SignalRHubs.Extensions;
using UZonMail.Core.SignalRHubs.SendEmail;
using UZonMail.DB.Extensions;
using UZonMail.DB.SQL;
using UZonMail.DB.SQL.Core.EmailSending;

namespace UZonMail.Core.Services.SendCore.ResponsibilityChains
{
    public class OutboxesPostHandler(GroupTasksList groupTasksList, OutboxesManager outboxManager, AppSettingsManager settingsService) : AbstractSendingHandler
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(OutboxesPostHandler));

        protected override async Task HandleCore(SendingContext context)
        {
            // 移除发件箱：
            // 1. 发件箱错误
            // 2. 发件数量达到上限
            // 3. 无件可发。特定发件箱,没有件可发; 非特定发件箱，发件组已清除

            // 若因为发件箱移除，需要同时移除发件组时，还要下发发件组状态变更通知

            var outbox = context.OutboxAddress;
            var emailItem = context.EmailItem;

            // outbox 要延迟到
            if (outbox == null) return;
            if (emailItem == null)
            {
                // 没有发件项时，可会存在所有发件正在发送中的情况，因此 outbox 不能立马释放, 需要进行判断
                if (!MatchEmailItem(outbox))
                {
                    var message = "未匹配到可发邮件,主动释放";
                    _logger.Info(message);
                    // 移除
                    outboxManager.RemoveOutbox(outbox, message);
                }
                return;
            }

            // 增加发件数量
            if (emailItem.IsErrorOrSuccess())
            {
                // 判断是否达到了最大的发件数限制
                outbox.IncreaseSentCount();

                // 从发件箱中移除发件项
                outbox.RemoveSepecificSendingItem(emailItem.SendingItem.SendingGroupId, emailItem.SendingItemId);
            }

            // 检查发件箱发件数量是否超限
            // 若超限，则标记为需要释放
            await CheckOutboxSentCountLimit(context.SqlContext, outbox);

            // 发件箱被标记为需要释放
            if (outbox.ShouldDispose)
            {
                // 移除对应的发件组中的数据
                // 1. 特定发件箱，移除特定邮件
                // 2. 共享发件箱，判断是否还有多余的发件箱，若没有，则整体移除
                await RemoveLinkingGroups(context, outbox);
            }
        }

        /// <summary>
        /// 判断发件箱是否还有邮件需要发
        /// </summary>
        /// <param name="outbox"></param>
        /// <returns></returns>
        private bool MatchEmailItem(OutboxEmailAddress outbox)
        {
            if (!groupTasksList.TryGetValue(outbox.UserId, out var groupTasks)) return false;
            return groupTasks.MatchEmailItem(outbox);
        }

        /// <summary>
        /// 检查发件箱的发件数量限制
        /// </summary>
        private async Task CheckOutboxSentCountLimit(SqlContext sqlContext, OutboxEmailAddress outbox)
        {
            var orgSetting = await settingsService.GetSetting<SendingSetting>(sqlContext, outbox.UserId);

            // 本身有限制时，若已经达到发送上限，则不再发送
            var overflowLimit = false;
            if (outbox.MaxSendCountPerDay > 0)
            {
                if (outbox.SentTotalToday > outbox.MaxSendCountPerDay)
                {
                    overflowLimit = true;
                }
            }
            // 本身没限制，使用系统的限制
            else if (orgSetting.MaxSendCountPerEmailDay > 0 && outbox.SentTotalToday >= orgSetting.MaxSendCountPerEmailDay)
            {
                overflowLimit = true;
            }

            if (overflowLimit)
            {
                var message = $"发件箱 {outbox.Email} 已达当日最大发件量: ${outbox.SentTotalToday}";
                _logger.Warn(message);
                outbox.MarkShouldDispose(message);
            }
        }

        /// <summary>
        /// 移除关联的发件组
        /// 这个方法必须在 outbox 被移除后，才能调用
        /// </summary>
        /// <param name="sqlContext"></param>
        /// <param name="outbox"></param>
        /// <returns></returns>
        private async Task RemoveLinkingGroups(SendingContext sendingContext, OutboxEmailAddress outbox)
        {
            // 受影响的发件任务           
            if (!groupTasksList.TryGetValue(outbox.UserId, out var groupTasks)) return;

            var sqlContext = sendingContext.SqlContext;
            var hub = sendingContext.HubClient;
            var client = hub.GetUserClient(outbox.UserId);

            // 移除指定发件箱的发件项
            var sendingGroupIds = outbox.GetSendingGroupIds();
            foreach (var sendingGroupId in sendingGroupIds)
            {
                // 获取发件组
                if (!groupTasks.TryGetValue(sendingGroupId, out var groupTask)) continue;

                // 判断当前发件组是否还有发件箱
                var existOutboxes = outboxManager.ExistOutboxes(sendingGroupId);
                if (existOutboxes)
                {
                    var sendingItemIds = outbox.GetSpecificSendingItemIds();

                    // 从发件组中移除发件箱
                    groupTask.RemovePendingItems(sendingItemIds);

                    // 标记为错误
                    await sqlContext.SendingItems.UpdateAsync(x => x.SendingGroupId == sendingGroupId && sendingItemIds.Contains(x.Id),
                        x => x.SetProperty(y => y.Status, SendingItemStatus.Failed)
                            .SetProperty(y => y.SendDate, DateTime.UtcNow)
                            .SetProperty(y => y.SendResult, outbox.ErroredMessage ?? "发件箱退出发件池，无发件箱可用")
                        );

                    // 更新发件组成功的数据
                    var sendingGroup = await SendingGroupUpdater.UpdateSendingGroupSentInfo(sqlContext, sendingGroupId);
                    // 推送发送组进度
                    await client.SendingGroupProgressChanged(new SendingGroupProgressArg(sendingGroup, sendingContext.GroupTaskStartDate));
                    continue;
                }
                ;

                // 发件组不存在任何发件箱时，需要移除整个发件组
                if (!groupTasks.TryRemove(sendingGroupId, out var removedGroupTask)) continue;
                // 修改发件项状态
                await sendingContext.SqlContext.SendingItems.UpdateAsync(x => x.SendingGroupId == sendingGroupId && x.Status == SendingItemStatus.Pending
                , x => x.SetProperty(y => y.Status, SendingItemStatus.Failed)
                    .SetProperty(y => y.SendDate, DateTime.UtcNow)
                    .SetProperty(y => y.SendResult, outbox.ErroredMessage ?? "发件箱退出发件池，无发件箱可用")
                );

                // 更新发件组成功的数据
                var removedSendingGroup = await SendingGroupUpdater.UpdateSendingGroupSentInfo(sqlContext, sendingGroupId);

                // 标记结束
                var finisher = sendingContext.Provider.GetRequiredService<SendingGroupFinisher>();
                await finisher.MarkSendingGroupFinished(removedSendingGroup.Id, sendingContext.GroupTaskStartDate);
            }
        }
    }
}
