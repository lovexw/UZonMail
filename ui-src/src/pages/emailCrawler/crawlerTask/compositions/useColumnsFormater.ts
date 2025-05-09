import { CrawlerStatus, CrawlerType } from 'src/api/pro/crawlerTask'
import type { ITikTokDevice } from 'src/api/pro/tikTokDevice';
import { getAllUserTikTokDevices } from 'src/api/pro/tikTokDevice'
import type { IProxy } from 'src/api/proxy';
import { getUsableProxies } from 'src/api/proxy'

export function useColumnsFormater () {
  const usableProxies: Ref<IProxy[]> = ref([])
  const usableDevices: Ref<ITikTokDevice[]> = ref([])

  // 获取用户的代理
  onMounted(async () => {
    const { data: proxies } = await getUsableProxies()
    usableProxies.value = proxies

    const { data: devices } = await getAllUserTikTokDevices()
    usableDevices.value = devices
  })

  /**
   * 格式化代理id
   * @param proxyId
   * @returns
   */
  function formatProxyId (proxyId: number) {
    return usableProxies.value.find(x => x.id === proxyId)?.name || ''
  }

  function formatCrawlerType (value: number) {
    return CrawlerType[value] as string
  }

  function formatCrawlerStatus (value: number) {
    return CrawlerStatus[value] as string
  }

  function formatDeviceId (deviceId: number) {
    return usableDevices.value.find(x => x.id === deviceId)?.name || ''
  }

  return {
    formatProxyId,
    formatCrawlerType,
    formatCrawlerStatus,
    formatDeviceId,
    usableProxies,
    usableDevices
  }
}
