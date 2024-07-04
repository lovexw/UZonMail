﻿using log4net;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Uamazing.Utils.Web.Service;
using UZonMailService.Models.SQL;
using UZonMailService.Models.SQL.EmailSending;
using UZonMailService.Models.SQL.MultiTenant;
using UZonMailService.Services.EmailSending.Base;
using UZonMailService.Services.EmailSending.Event;
using UZonMailService.Services.EmailSending.Event.Commands;
using UZonMailService.Services.EmailSending.OutboxPool;
using UZonMailService.Services.EmailSending.Pipeline;
using UZonMailService.Services.EmailSending.Sender;
using UZonMailService.Services.Settings;
using UZonMailService.SignalRHubs;
using UZonMailService.Utils.Database;

namespace UZonMailService.Services.EmailSending.WaitList
{
    /// <summary>
    /// 系统级的待发件调度器
    /// 请求时，平均向各个用户请求资源
    /// 每位用户的资源都是公平的
    /// 今后可以考虑加入权重
    /// </summary>
    public class UserSendingGroupsManager(IServiceScopeFactory ssf) : ISingletonService
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(UserSendingGroupsManager));
        private readonly ConcurrentDictionary<long, UserSendingGroupsPool> _userTasks = new();

        /// <summary>
        /// 将发件组添加到待发件队列
        /// 内部会自动向前端发送消息通知
        /// 内部已调用 CreateSendingGroup
        /// </summary>
        /// <param name="sendingContext">数据库上文</param>
        /// <param name="group"></param>
        /// <param name="sendingItemIds"></param>
        /// <returns></returns>
        public async Task<bool> AddSendingGroup(SendingContext sendingContext, SendingGroup group, List<long>? sendingItemIds = null)
        {
            if (group == null)
                return false;
            if (group.SmtpPasswordSecretKeys == null || group.SmtpPasswordSecretKeys.Count != 2)
            {
                _logger.Warn($"发送 {group.Id} 时, 没有提供 smtp 解密密钥，取消发送");
                return false;
            }

            // 判断是否有用户发件管理器
            if (!_userTasks.TryGetValue(group.UserId, out var taskManager))
            {
                // 新建用户发件管理器
                taskManager = new UserSendingGroupsPool(group.UserId);
                _userTasks.TryAdd(group.UserId, taskManager);
            }

            // 向发件管理器添加发件组
            bool result = await taskManager.AddSendingGroup(sendingContext, group.Id, group.SmtpPasswordSecretKeys, sendingItemIds);
            return result;
        }

        /// <summary>
        /// 发送模块调用该方法获取发件项
        /// 若返回空，会导致发送任务暂停
        /// </summary>
        /// <returns></returns>
        public async Task<SendItem?> GetSendItem(SendingContext sendingContext, OutboxEmailAddress outbox)
        {
            if (_userTasks.Count == 0)
                return null;
            var userId = outbox.UserId;

            // 依次获取发件项
            // 返回 null 有以下几种情况：
            // 1. manager 为空
            // 2. 所有发件箱都在冷却中
            if (!_userTasks.TryGetValue(userId, out var sendingGroupsPool))
            {
                // 用户已经没有发件任务，移除发件箱池
                await new DisposeUserOutboxPoolCommand(sendingContext, userId).Execute(this);
                return null;
            }

            // 为空时移除
            if (sendingGroupsPool.Count == 0)
            {
                // 移除自己
                _userTasks.TryRemove(userId, out _);
                // 移除用户发件池
                await new DisposeUserOutboxPoolCommand(sendingContext, userId).Execute(this);
                return null;
            }

            // 保存当前引用
            sendingContext.UserSendingGroupsManager = this;
            return await sendingGroupsPool.GetSendItem(sendingContext, outbox);
        }

        /// <summary>
        /// 邮件项发送完成回调
        /// </summary>
        /// <param name="sendingContext"></param>
        /// <returns></returns>
        public async Task EmailItemSendCompleted(SendingContext sendingContext)
        {
            // 清除数据
            // 移除用户队列池
            if (sendingContext.UserSendingGroupsPool.Count == 0)
            {
                _userTasks.TryRemove(sendingContext.UserSendingGroupsPool.UserId, out _);                
            }

            // 回调发件箱处理
            await sendingContext.OutboxEmailAddress.EmailItemSendCompleted(sendingContext);
        }

        public async Task RemoveSendingGroupTask(long userId,long sendingGroupId)
        {
            if (!_userTasks.TryGetValue(userId, out var userSendingGroupsPool)) return;
            userSendingGroupsPool.TryRemove(sendingGroupId, out _);
        }
    }
}
