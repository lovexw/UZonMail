# 开发说明

## 对应关系

一个邮箱对应一个 SmtpClient，一个 SmtpClient 也对应一个 ProxyClient。

## 未解决问题

若两个用户同时使用同一个邮箱时，邮箱冷却如何共用，设置如何独立

目前看来无解，唯有限制一个邮箱只能一个人使用。

但这又有一个问题，其他用户可能会恶意绑定邮箱，导致另外的用户无法使用(目前看来这种概率很小

若不限制邮箱使用人，则会出现后者覆盖前者的情况，但谁会把账号借给另一个人用而承担这个风险呢，所以此处暂时不考虑