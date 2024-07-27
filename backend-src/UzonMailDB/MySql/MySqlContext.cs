﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UZonMail.DB.SQL;

namespace UZonMail.DB.MySql
{
    public class MySqlContext : SqlContext
    {
        private readonly MySqlConnectionConfig _mysqlConnectionConfig;
        public MySqlContext(IConfiguration configuration)
        {
            _mysqlConnectionConfig = new();
            configuration.GetSection("Database:MySql").Bind(_mysqlConnectionConfig);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseMySql(_mysqlConnectionConfig.ConnectionString, new MySqlServerVersion(_mysqlConnectionConfig.MysqlVersion));
        }
    }
}
