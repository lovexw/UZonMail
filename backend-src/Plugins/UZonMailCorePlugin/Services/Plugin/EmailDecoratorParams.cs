﻿using System;
using UZonMail.DB.Managers.Cache;
using UZonMail.DB.SQL.Core.EmailSending;
using UZonMail.Utils.Email;

namespace UZonMail.Core.Services.Plugin
{
    public class EmailDecoratorParams(OrganizationSettingCache settingReader, SendingItem sendingItem, string outboxEmail) : IEmailDecoratorParams
    {
        public OrganizationSettingCache SettingsReader { get; } = settingReader;

        public SendingItem SendingItem { get; } = sendingItem;

        /// <summary>
        /// 发件箱
        /// </summary>
        public string OutboxEmail { get; set; } = outboxEmail;
    }
}
