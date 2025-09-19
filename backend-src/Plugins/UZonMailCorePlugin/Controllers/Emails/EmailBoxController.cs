﻿using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Uamazing.Utils.Web.ResponseModel;
using UZonMail.Core.Database.Validators;
using UZonMail.Core.Services.Emails;
using UZonMail.Core.Services.Encrypt;
using UZonMail.Core.Services.Settings;
using UZonMail.Core.Services.UserInfos;
using UZonMail.Core.Utils.Extensions;
using UZonMail.DB.Extensions;
using UZonMail.DB.SQL;
using UZonMail.DB.SQL.Core.Emails;
using UZonMail.Utils.Web.Exceptions;
using UZonMail.Utils.Web.PagingQuery;
using UZonMail.Utils.Web.ResponseModel;

namespace UZonMail.Core.Controllers.Emails
{
    /// <summary>
    /// 邮箱
    /// </summary>
    public class EmailBoxController(SqlContext db, TokenService tokenService, UserService userService,
        EmailGroupService emailGroupService, OutboxValidateService emailUtils, EncryptService encryptService) : ControllerBaseV1
    {
        /// <summary>
        /// 创建发件箱
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost("outbox")]
        public async Task<ResponseResult<Outbox>> CreateOutbox([FromBody] Outbox entity)
        {
            var outboxValidator = new OutboxValidator();
            var vdResult = outboxValidator.Validate(entity);
            if (!vdResult.IsValid)
            {
                return vdResult.ToErrorResponse<Outbox>();
            }

            // 设置默认端口
            if (entity.SmtpPort == 0) entity.SmtpPort = 25; // 默认端口

            var userId = tokenService.GetUserSqlId();
            // 验证发件箱是否存在，若存在，则复用原来的发件箱
            Outbox? existOne = db.Outboxes.SingleOrDefault(x => x.UserId == userId && x.Email == entity.Email);
            if (existOne != null)
            {
                existOne.EmailGroupId = entity.EmailGroupId;
                existOne.SmtpPort = entity.SmtpPort;
                existOne.Password = entity.Password;
                existOne.UserName = entity.UserName;
                existOne.Description = entity.Description;
                existOne.ProxyId = entity.ProxyId;
                existOne.ReplyToEmails = entity.ReplyToEmails;
                existOne.SetStatusNormal();
            }
            else
            {
                // 新建一个发件箱
                entity.UserId = userId;
                db.Outboxes.Add(entity);
                existOne = entity;
            }
            await db.SaveChangesAsync();

            return existOne.ToSuccessResponse();
        }

        /// <summary>
        /// 批量新增发件箱
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost("outboxes")]
        public async Task<ResponseResult<List<Outbox>>> CreateOutboxes([FromBody] List<Outbox> entities)
        {
            if (entities == null)
            {
                return ResponseResult<List<Outbox>>.Fail("未能解析发件箱数据");
            }
            var userId = tokenService.GetUserSqlId();
            foreach (var entity in entities)
            {
                // 设置默认端口
                if (entity.SmtpPort == 0) entity.SmtpPort = 25;
                // 设置用户
                entity.UserId = userId;

                // 验证数据
                var outboxValidator = new OutboxValidator();
                var vdResult = outboxValidator.Validate(entity);
                if (!vdResult.IsValid)
                {
                    return vdResult.ToErrorResponse<List<Outbox>>();
                }
            }

            List<string> emails = entities.Select(x => x.Email).ToList();
            List<Outbox> existEmails = await db.Outboxes.Where(x => x.UserId == userId && emails.Contains(x.Email)).ToListAsync();
            List<Outbox?> newEntities = emails.Except(existEmails.Select(x => x.Email))
                .Select(x => entities.Find(e => e.Email == x))
                .ToList();

            // 新建发件箱
            await db.Outboxes.AddRangeAsync(newEntities.Where(x => x != null));

            // 更新现有的发件箱
            foreach (var entity in existEmails)
            {
                var newEntity = entities.Find(x => x.Email == entity.Email);
                if (newEntity != null)
                {
                    entity.EmailGroupId = newEntity.EmailGroupId;
                    entity.SmtpPort = newEntity.SmtpPort;
                    entity.UserName = newEntity.UserName;
                    entity.Password = newEntity.Password;
                    entity.EnableSSL = newEntity.EnableSSL;
                    entity.Description = newEntity.Description;
                    entity.ProxyId = newEntity.ProxyId;
                    entity.Name = newEntity.Name;
                    entity.ReplyToEmails = newEntity.ReplyToEmails;
                    entity.SetStatusNormal();
                }
            }
            await db.SaveChangesAsync();

            // 返回所有的结果
            List<Outbox> results = [.. existEmails, .. newEntities];
            return results.ToSuccessResponse();
        }

        /// <summary>
        /// 创建发件箱
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost("inbox")]
        public async Task<ResponseResult<Inbox>> CreateInbox([FromBody] Inbox entity)
        {
            var inboxValidator = new InboxValidator();
            var vdResult = inboxValidator.Validate(entity);
            if (!vdResult.IsValid) return vdResult.ToErrorResponse<Inbox>();

            var tokenPayloads = tokenService.GetTokenPayloads();
            var userId = tokenPayloads.UserId;
            entity.UserId = userId;
            entity.OrganizationId = tokenPayloads.OrganizationId;

            // 验证发件箱是否存在，若存在，则复用原来的发件箱
            Inbox? existOne = db.Inboxes.IgnoreQueryFilters().SingleOrDefault(x => x.UserId == userId && x.Email == entity.Email);
            if (existOne != null)
            {
                existOne.EmailGroupId = entity.EmailGroupId;
                existOne.Name = entity.Name;
                existOne.Description = entity.Description;
                existOne.SetStatusNormal();
            }
            else
            {
                // 新建一个发件箱               
                db.Inboxes.Add(entity);
                existOne = entity;
            }
            await db.SaveChangesAsync();

            return existOne.ToSuccessResponse();
        }

        /// <summary>
        /// 添加未分组收件箱
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        /// <exception cref="KnownException"></exception>
        [HttpPost("inbox/ungrouped")]
        public async Task<ResponseResult<Inbox>> CreateUngroupedInbox([FromBody] Inbox entity)
        {
            // 获取未分组的组
            var defaultGroup = await emailGroupService.GetDefaultEmailGroup(EmailGroupType.InBox);
            entity.EmailGroupId = defaultGroup.Id;

            return await CreateInbox(entity);
        }

        /// <summary>
        /// 批量新增发件箱
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost("inboxes")]
        public async Task<ResponseResult<List<Inbox>>> CreateInboxes([FromBody] List<Inbox> entities)
        {
            if (entities == null)
            {
                return ResponseResult<List<Inbox>>.Fail("未能解析收件箱数据");
            }

            var userId = tokenService.GetUserSqlId();
            foreach (var entity in entities)
            {
                // 设置用户
                entity.UserId = userId;
                var inboxValidator = new InboxValidator();
                var vdResult = inboxValidator.Validate(entity);
                if (!vdResult.IsValid) return vdResult.ToErrorResponse<List<Inbox>>();
            }

            List<string> emails = entities.Select(x => x.Email).ToList();
            List<Inbox> existEmails = await db.Inboxes.IgnoreQueryFilters().Where(x => x.UserId == userId && emails.Contains(x.Email)).ToListAsync();
            List<Inbox?> newEntities = emails.Except(existEmails.Select(x => x.Email))
                .Select(x => entities.Find(e => e.Email == x))
                .ToList();

            // 新建发件箱
            foreach (var entity in newEntities)
            {
                if (entity != null)
                    db.Inboxes.Add(entity);
            }

            // 更新现有的发件箱
            foreach (var entity in existEmails)
            {
                var newEntity = entities.Find(x => x.Email == entity.Email);
                if (newEntity != null)
                {
                    entity.EmailGroupId = newEntity.EmailGroupId;
                    entity.Name = newEntity.Name;
                    entity.Description = newEntity.Description;
                    entity.SetStatusNormal();
                }
            }
            await db.SaveChangesAsync();

            // 返回所有的结果
            List<Inbox> results = [.. existEmails, .. newEntities];
            return results.ToSuccessResponse();
        }

        /// <summary>
        /// 更新发件箱
        /// </summary>
        /// <param name="outboxId"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPut("outbox/{outboxId:long}")]
        public async Task<ResponseResult<bool>> UpdateOutbox(long outboxId, [FromBody] Outbox entity)
        {
            await db.Outboxes.UpdateAsync(x => x.Id == outboxId,
                 x => x.SetProperty(y => y.Email, entity.Email)
                 .SetProperty(y => y.Name, entity.Name)
                 .SetProperty(y => y.SmtpHost, entity.SmtpHost)
                 .SetProperty(y => y.SmtpPort, entity.SmtpPort)
                 .SetProperty(y => y.UserName, entity.UserName)
                 .SetProperty(y => y.Password, entity.Password)
                 .SetProperty(y => y.EnableSSL, entity.EnableSSL)
                 .SetProperty(y => y.Description, entity.Description)
                 .SetProperty(y => y.ProxyId, entity.ProxyId)
                 .SetProperty(y => y.ReplyToEmails, entity.ReplyToEmails)
                 );
            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 测试发件箱是否可用
        /// </summary>
        /// <param name="outboxId"></param>
        /// <returns></returns>
        /// <exception cref="KnownException"></exception>
        [HttpPut("outbox/{outboxId:long}/validation")]
        public async Task<ResponseResult<bool>> ValidateOutbox(long outboxId)
        {
            var result = await emailUtils.ValidateOutbox(outboxId);
            // 让前端自己处理错误
            result.Ok = true;
            return result;
        }

        /// <summary>
        /// 更新收件箱
        /// </summary>
        /// <param name="inboxId"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPut("inbox/{inboxId:long}")]
        public async Task<ResponseResult<bool>> UpdateInbox(long inboxId, [FromBody] Inbox entity)
        {
            await db.Inboxes.UpdateAsync(x => x.Id == inboxId,
                 x => x.SetProperty(y => y.Email, entity.Email)
                    .SetProperty(y => y.Name, entity.Name)
                    .SetProperty(y => y.MinInboxCooldownHours, entity.MinInboxCooldownHours)
                    .SetProperty(y => y.Description, entity.Description)
                 );
            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 获取邮箱数量
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="emailBoxType"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpGet("outbox/filtered-count")]
        public async Task<ResponseResult<int>> GetOutboxesCount(long groupId, string filter)
        {
            var userId = tokenService.GetUserSqlId();

            // 收件箱
            var dbSet = db.Outboxes.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted && !x.IsHidden);
            if (groupId > 0)
            {
                dbSet = dbSet.Where(x => x.EmailGroupId == groupId);
            }
            if (!string.IsNullOrEmpty(filter))
            {
                dbSet = dbSet.Where(x => x.Email.Contains(filter) || x.Description.Contains(filter));
            }
            int count = await dbSet.CountAsync();
            return count.ToSuccessResponse();
        }

        /// <summary>
        /// 获取邮箱数据
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="emailBoxType"></param>
        /// <param name="filter"></param>
        /// <param name="pagination"></param>
        /// <returns></returns>
        [HttpPost("outbox/filtered-data")]
        public async Task<ResponseResult<List<Outbox>>> GetOutboxesData(long groupId, string filter, [FromBody] Pagination pagination)
        {
            var userId = tokenService.GetUserSqlId();
            var dbSet = db.Outboxes.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted && !x.IsHidden);
            if (groupId > 0)
            {
                dbSet = dbSet.Where(x => x.EmailGroupId == groupId);
            }
            if (!string.IsNullOrEmpty(filter))
            {
                dbSet = dbSet.Where(x => x.Email.Contains(filter) || x.Description.Contains(filter));
            }
            var results = await dbSet.Page(pagination).ToListAsync();
            return results.ToSuccessResponse();
        }

        /// <summary>
        /// 获取发件箱信息
        /// </summary>
        /// <param name="outboxId"></param>
        /// <returns></returns>
        [HttpGet("outboxes/{outboxId:long}")]
        public async Task<ResponseResult<Outbox>> GetOutboxInfo(long outboxId)
        {
            var userId = tokenService.GetUserSqlId();
            var outbox = await db.Outboxes.Where(x => x.Id == outboxId && x.UserId == userId).FirstOrDefaultAsync();
            return outbox.ToSuccessResponse();
        }

        /// <summary>
        /// 通过 id 删除邮箱
        /// 若邮箱在使用，则仅标记一个删除状态
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpDelete("outboxes/{emailBoxId:long}")]
        public async Task<ResponseResult<bool>> DeleteOutboxById(long emailBoxId)
        {
            var emailBox = await db.Outboxes.FirstOrDefaultAsync(x => x.Id == emailBoxId);
            if (emailBox == null) throw new KnownException("邮箱不存在");
            db.Outboxes.Remove(emailBox);
            await db.SaveChangesAsync();

            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 批量删除多个发件箱
        /// </summary>
        /// <param name="emailBoxId"></param>
        /// <returns></returns>
        /// <exception cref="KnownException"></exception>
        [HttpDelete("outboxes/ids")]
        public async Task<ResponseResult<bool>> DeleteOutboxByIds([FromBody] List<string> outboxIds)
        {
            var userId = tokenService.GetUserSqlId();

            var emailBox = db.Outboxes.Where(x => x.UserId == userId && outboxIds.Contains(x.ObjectId));
            db.Outboxes.RemoveRange(emailBox);
            await db.SaveChangesAsync();

            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 获取邮箱数量
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="emailBoxType"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpGet("inbox/filtered-count")]
        public async Task<ResponseResult<int>> GetInboxesCount(long groupId, string filter)
        {
            var userId = tokenService.GetUserSqlId();

            // 收件箱
            var dbSet = db.Inboxes.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted && !x.IsHidden);
            if (groupId > 0)
            {
                dbSet = dbSet.Where(x => x.EmailGroupId == groupId);
            }
            if (!string.IsNullOrEmpty(filter))
            {
                dbSet = dbSet.Where(x => x.Email.Contains(filter) || x.Description.Contains(filter));
            }
            int count = await dbSet.CountAsync();
            return count.ToSuccessResponse();
        }

        /// <summary>
        /// 获取邮箱数据
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="emailBoxType"></param>
        /// <param name="filter"></param>
        /// <param name="pagination"></param>
        /// <returns></returns>
        [HttpPost("inbox/filtered-data")]
        public async Task<ResponseResult<List<Inbox>>> GetInboxesData(long groupId, string filter, [FromBody] Pagination pagination)
        {
            var userId = tokenService.GetUserSqlId();
            var dbSet = db.Inboxes.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted && !x.IsHidden);
            if (groupId > 0)
            {
                dbSet = dbSet.Where(x => x.EmailGroupId == groupId);
            }
            if (!string.IsNullOrEmpty(filter))
            {
                dbSet = dbSet.Where(x => x.Email.Contains(filter) || x.Description.Contains(filter));
            }
            var results = await dbSet.Page(pagination).ToListAsync();
            return results.ToSuccessResponse();
        }

        /// <summary>
        /// 通过组获取收件箱
        /// </summary>
        /// <param name="groupIds"></param>
        /// <returns></returns>
        [HttpGet("inbox/groups-data")]
        public async Task<ResponseResult<List<Inbox>>> GetGroupsInboxes(string groupIds)
        {
            var longGroupIds = groupIds.Split(",")
                .Select(x =>
                    {
                        if (long.TryParse(x, out var value)) return value;
                        return 0;
                    })
                .Where(x => x > 0)
                .ToList();
            if (longGroupIds.Count == 0)
            {
                return ResponseResult<List<Inbox>>.Success([]);
            }

            var userId = tokenService.GetUserSqlId();
            var inboxes = await db.Inboxes.AsNoTracking()
                .Where(x => longGroupIds.Contains(x.EmailGroupId))
                .Where(x => x.UserId == userId)
                .Select(x => new Inbox()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email
                })
                .ToListAsync();

            return inboxes.ToSuccessResponse();
        }

        /// <summary>
        /// 通过 id 删除邮箱
        /// 若邮箱在使用，则仅标记一个删除状态
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpDelete("inboxes/{emailBoxId:long}")]
        public async Task<ResponseResult<bool>> DeleteInboxById(long emailBoxId)
        {
            var emailBox = await db.Inboxes.FirstOrDefaultAsync(x => x.Id == emailBoxId) ?? throw new KnownException("邮箱不存在");
            emailBox.IsDeleted = true;
            await db.SaveChangesAsync();

            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 批量删除多个收件箱
        /// </summary>
        /// <param name="inboxIds"></param>
        /// <returns></returns>
        [HttpDelete("inboxes/ids")]
        public async Task<ResponseResult<bool>> DeleteInboxByIds([FromBody] List<string> inboxIds)
        {
            var userId = tokenService.GetUserSqlId();

            var emailBox = db.Inboxes.Where(x => x.UserId == userId && inboxIds.Contains(x.ObjectId));
            db.Inboxes.RemoveRange(emailBox);
            await db.SaveChangesAsync();

            return true.ToSuccessResponse();
        }

        /// <summary>
        /// 获取加密后的发件箱密码
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpGet("encrypt-password")]
        public async Task<ResponseResult<string>> EncryptOutboxPassword(string password)
        {
            var userId = tokenService.GetUserSqlId();
            if (string.IsNullOrEmpty(password)) throw new KnownException("密码不能为空");

            var encryptedResult = encryptService.EncryptOutboxSecret(userId, password);
            return encryptedResult.ToSuccessResponse();
        }
    }
}
