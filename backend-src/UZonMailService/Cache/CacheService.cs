﻿using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using UZonMailService.Config;
using Uamazing.Utils.Json;
using Newtonsoft.Json.Linq;

namespace UZonMailService.Cache
{
    /// <summary>
    /// 获取缓存服务
    /// 若 redis 不可用，则使用内存作为缓存
    /// 该服务通过 UseCacheExtension 注入
    /// !!! 不要直接在逻辑中直接调用该服务，应该将接口封装后再使用
    /// </summary>
    public class CacheService
    {
        /// <summary>
        /// 缓存服务单例
        /// </summary>
        public static CacheService Instance { get; private set; }

        /// <summary>
        /// Redis 是否可用
        /// </summary>
        public bool RedisEnabled { get; private set; }
        /// <summary>
        /// redis 数据库
        /// </summary>
        public IDatabaseAsync RedisCache { get; private set; }

        /// <summary>
        /// 基于内存的数据库，当 redis 不可用时，使用该数据库
        /// </summary>
        public IMemoryCache MemoryCache { get; private set; }

        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public CacheService(IConfiguration configuration)
        {
            // 供外部调用
            Instance = this;

            // 初始化内存缓存
            MemoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

            // 初始化 Redis
            var redisConfig = new RedisConnectionConfig();
            configuration.GetSection("Database:Redis").Bind(redisConfig);
            if (!redisConfig.Enable)
            {
                RedisEnabled = false;
                return;
            }

            _connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfig.ConnectionString);
            _connectionMultiplexer.ConnectionFailed += (sender, args) =>
            {
                // 链接失败
                RedisEnabled = false;
            };
            _connectionMultiplexer.ConnectionRestored += (sender, args) =>
            {
                // 链接成功
                RedisEnabled = true;
                RedisCache = _connectionMultiplexer.GetDatabase(redisConfig.Database);
            };
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<bool> SetAsync<T>(string key, T? value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return false;

            if (RedisEnabled)
            {
                // 将数据转为 json
                return await RedisCache.SetAddAsync(key, value.ToJson());
            }
            else
            {
                MemoryCache.Set(key, value);
                return true;
            }
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<T?> GetAsync<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                return default;

            if (RedisEnabled)
            {
                // 将数据转为 json
                var value = await RedisCache.StringGetAsync(key);
                return value.ToString().JsonTo<T>();
            }
            else
            {
                return MemoryCache.Get<T>(key);
            }
        }
    }
}
