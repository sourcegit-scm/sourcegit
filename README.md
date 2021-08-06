# SourceGit

开源的Git客户端，仅用于Windows。

## 特点

* 永久免费+开源
* 轻量级，Windows 10下软件小于2M，无需安装，直接运行
* 启动速度、加载速度快（相对于SmartGit，SourceTree等，加载同数量的日志）
* 一次性显示最近20000条提交历史
* 中英双语并提供明暗两种主题
* 覆盖常用GIT指令
* 分支线路图
* 高级指令图形操作
  * SUBMODULES
  * SUBTREES
  * ARCHIVE
  * PATCH/APPLY
  * FILE HISTORIES
  * BLAME
  * REVISION DIFF

## 下载

下载地址：[发行版](https://gitee.com/sourcegit/sourcegit/releases/)

| 文件             | 运行时             | 说明                                |
| ---------------- | ------------------ | ----------------------------------- |
| SourceGit.exe    | .NET 5             | 需要自行安装 .NET 5运行时           |
| SourceGit_48.exe | .NET Framework 4.8 | Windows 10 内置该运行时，不需要安装 |

> 【注】本软件为GIT的**图形前端**，需先自行安装Git

## 预览

* 暗色主题

![Theme Dark](./screenshots/theme_dark.png)

* 亮色主题

![Theme Light](./screenshots/theme_light.png)

## Thanks

* [Jai](https://gitee.com/abel) .NET Framework 4.8 与 .NET 5 平台使用System.Text.Json统一代码
* [Jai](https://gitee.com/abel) 修复SUBMODULE路径不正确的BUG
* [Jai](https://gitee.com/abel) 修复刷新分支时，分支树节点状态未正常恢复的BUG
* [PUMA](https://gitee.com/whgfu) 配置默认User
* [Rwing](https://gitee.com/rwing) GitFlow: add an option to keep branch after finish
* [XiaoLinger](https://gitee.com/LingerNN) 纠正弹出框文本配置方式
* [Jai](https://gitee.com/abel) 启动恢复上次浏览页面功能
* [Jai](https://gitee.com/abel) 修复不同remote分支结构生成错误的BUG
