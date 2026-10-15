# 安徽大学校园网助手 Windows 1.3.0

Windows 10 / 11 中文桌面工具：账号、密码、连接、重试、取消和连接状态，支持可选的开机自启动和启动软件后自动连接。

- [下载 Windows v1.3.0](https://github.com/fatguy0928/AHU-Campus-Network-Helper/releases/tag/windows-v1.3.0)
- [下载 Android v1.0.3](https://github.com/fatguy0928/AHU-Campus-Network-Helper/releases/tag/v1.0.3)（Android 8 及以上）

程序图标使用[安徽大学官网发布的校徽](https://www.ahu.edu.cn/_upload/article/images/45/88/cc4b58214d3eb0f7214755ac5d18/e9c35651-9625-423c-957b-0f1cd5bf1cfc.jpg)，生成 16、24、32、48、64、128、256 像素的 Windows 图标资源。

## 使用

1. 先连上ahu.portal，或插上校园网网线。
2. 双击 AHU校园网助手-v1.3.0.exe，输入与学校网页相同的账号和密码。
3. “记住账号和密码”默认勾选。点击「连接」时，程序使用 Windows DPAPI 为当前 Windows 用户加密保存凭据。
4. 下次启动会自动填入。取消勾选会立即删除保存文件；“清除已保存信息”会同时清空输入框。
5. 初始状态点击「连接」。失败、超时或取消后，界面会切换到「重试」；重试会重新读取当前校园网 IP。修改账号或密码后会切回「连接」。
6. 请求处理中可以「取消」。如果认证请求已经发出，取消只停止等待，不会注销已建立的校园网连接。
7. 「开机自启动」默认关闭。勾选后，当前 Windows 用户登录时会打开软件；取消勾选即可关闭。
8. 「启动软件后自动连接」默认关闭。先勾选「记住账号和密码」并连接一次，再开启此项。之后每次打开软件会使用保存的凭据执行一次连接；失败、超时或取消后可手动重试。
9. 两个选项独立保存。要开机后自动连接，请同时开启它们。没有可用的保存凭据时会提示先保存，等待手动操作。

无需安装 Python，无需管理员权限，依赖 Windows 10/11 自带的 .NET Framework（4.5 或更高）。建议把 EXE 放到固定文件夹后再开启自启动；以后移动或重命名 EXE 时，取消并重新勾选「开机自启动」，以更新路径。

## 更新已有版本

关闭旧版后打开新版 EXE 即可。原来保存的账号密码可以继续使用。首次使用新版时，两个新增选项均为关闭状态，需手动勾选。

开机时校园 Wi-Fi 或网线尚未就绪，自动连接可能失败；连接校园网后点击「重试」即可。

## 状态含义

- **已连接**：网关接受认证，且联网检测返回了预期内容。
- **认证已接受 · 待确认外网**：网关接受认证或报告已在线，但外网检测未通过。可以打开普通网页确认。
- **连接未成功 / 连接超时**：检查 Wi-Fi/网线、账号密码，再点击重试；也可使用「打开学校网页登录」。

单个网络请求的超时上限为 8 秒。一次连接可能包含认证页读取、登录、两次外网检测，因此总耗时可能超过 8 秒。软件不会在失败后循环提交密码。

## 适配与验证

适配用户提供的安徽大学认证页 http://172.16.253.3/a79.htm ，AC 地址默认 172.16.253.1。
登录接口 http://172.16.253.3:801/eportal/?c=Portal&a=login 。
每次读取页面中的实时 IP，不把制作时的 IP 固定保存。

## 数据处理

账号密码不会写入日志。勾选“记住账号和密码”后，密文保存在 `%LOCALAPPDATA%\AHU Campus Helper\credentials.dat`，由 Windows DPAPI 绑定当前 Windows 用户；取消勾选或点击清除即可删除。登录遵循学校现有 HTTP 接口；登录请求不自动跟随重定向。
外网连通检测访问微软 www.msftconnecttest.com/connecttest.txt，不携带账号密码。
自动连接选项保存在 `%LOCALAPPDATA%\AHU Campus Helper\settings.json`，只包含开关，不包含账号密码。
勾选「开机自启动」时，在 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 写入本软件自己的 `AHU Campus Helper` 值，仅包含带引号的 EXE 完整路径。取消勾选只移除这一项；打开软件读取设置时不会添加或修改启动项。
程序自身请求直接访问网络，不修改 Windows 代理、网卡或计划任务。

## 源码构建与测试

Windows PowerShell 中执行：

    .\build.ps1
    .\test.ps1

编译产物在 dist；测试产物在 test-results。测试账号和密码为虚构数据。测试网络服务器仅监听本机回环地址。
源码使用系统 .NET Framework 的 csc.exe，不需要下载依赖。

## GitHub 参考

以下项目用于研究安大认证方式；本软件为独立编写的 C# 实现，没有附带或运行这些仓库的脚本、安装器。

- [C01in-0/AHU_AutoLogin](https://github.com/C01in-0/AHU_AutoLogin)：安徽大学 Dr.COM / ePortal 登录接口说明。
- [Biubush/AutoNet4AHU](https://github.com/Biubush/AutoNet4AHU)：安徽大学图形界面登录工具及认证参数示例。

参考日期：2026-09-14
