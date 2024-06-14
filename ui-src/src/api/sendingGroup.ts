/* eslint-disable @typescript-eslint/no-explicit-any */
import { httpClient } from 'src/api//base/httpClient'
import { IRequestPagination } from 'src/compositions/types'

/**
 * 发送组状态
 */
export enum SendingGroupStatus {
  /// <summary>
  /// 新建
  /// </summary>
  Created,

  /// <summary>
  /// 计划发件
  /// </summary>
  Scheduled,

  /// <summary>
  /// 发送中
  /// </summary>
  Sending,

  /// <summary>
  /// 暂停
  /// </summary>
  Pause,

  /// <summary>
  /// 停止
  /// </summary>
  Cancel,

  /// <summary>
  /// 发送完成
  /// </summary>
  Finish,
}

/**
 * 发送组类型
 */
export enum SendingGroupType {
  /// <summary>
  /// 即时发送
  /// </summary>
  Instant,

  /// <summary>
  /// 计划发送
  /// </summary>
  Scheduled,
}

export interface ISendingGroupInfo {
  id: number, // id
  userId: number, // 用户 id
  subjects: string, // 主题
  attachments: object[], // 附件
  totalCount: number, // 总数
  successCount: number, // 成功数
  status: SendingGroupStatus, // 状态
  sendStartDate: string, // 发送开始时间
}

/**
 * 发送组历史
 */
export interface ISendingGroupHistory extends ISendingGroupInfo {
  templatesCount: number, // 模板数量
  outboxesCount: number, // 发件人邮箱数量
  ccBoxesCount: number, // 抄送人邮箱数量
  bccBoxesCount: number, // 密送人邮箱数量
}

/**
 * 获取模板数量
 * @param filter
 * @returns
 */
export function getSendingGroupsCount (filter?: string) {
  return httpClient.get<number>('/sending-group/filtered-count', {
    params: {
      filter
    }
  })
}

/**
 * 获取模板数据
 * @param filter
 * @param pagination
 * @returns
 */
export function getEmailTemplatesData (filter: string | undefined, pagination: IRequestPagination) {
  return httpClient.post<ISendingGroupHistory[]>('/sending-group/filtered-data', {
    params: {
      filter
    },
    data: pagination
  })
}

export interface IRunningSendingGroup {
  id: number
  subjects: string
  progress: number,
  totalCount: number,
  sentCount: number,
  successCount?: number,
  status?: number
}

/**
 * 获取正在执行的发送组
 * @returns
 */
export function getRunningSendingGroups () {
  return httpClient.get<IRunningSendingGroup[]>('/sending-group/running')
}

/**
 * 获取发送组中的 subjects 值
 * @param sendingGroupId
 * @returns
 */
export function getSendingGroupSubjects (sendingGroupId: number) {
  return httpClient.get<string>(`/sending-group/${sendingGroupId}/subjects`)
}

export interface ISendingGroupStatusInfo {
  id: number
  progress: number,
  totalCount: number,
  sentCount: SendingGroupStatus,
  successCount?: number,
  status?: number
}

/**
 * 获取发送组中的 subjects 值
 * @param sendingGroupId
 * @returns
 */
export function getSendingGroupRunningInfo (sendingGroupId: number) {
  return httpClient.get<ISendingGroupStatusInfo>(`/sending-group/${sendingGroupId}/status-info`)
}
