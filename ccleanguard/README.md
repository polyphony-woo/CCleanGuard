# CCleanGuard

一个只做一件事的 Windows 小工具：**安全地清理 C 盘缓存**，并可选地每天提醒你一次。

- 单文件 EXE，32 KB，不依赖 PowerShell、不装运行时（.NET Framework 4.x 是 Windows 自带）
- 白名单硬编码，**白名单之外的东西一个字节都不动**
- **永不触碰 `pagefile.sys`**，不会影响虚拟内存
- 支持「立即清理」和「每日提醒」两种模式
- 所有日志写在 `%LOCALAPPDATA%\CCleanGuard\logs`，可随时删

> 先说清楚：这不是 CCleaner 那类注册表清洗工具。它只删**删了会自动重建**的缓存目录，
> 不做任何系统优化、不碰注册表、不卸载软件、不清理"隐私痕迹"。

---

## 为什么会有这个

512 GB 的笔记本用两年，C 盘莫名其妙就少了十几 GB。大部分不是你的文件，而是：

- 浏览器把一个 4 GB 的**端侧 AI 模型**下到了用户目录
- 编辑器攒了几 GB 的**符号索引缓存**
- 显卡驱动留了几 GB 的**着色器缓存**
- 每次更新留下的**安装包和补丁包**
- 你早就卸掉的软件留下的 **updater 残留**

这些东西都能安全删掉，但没人记得去删。CCleanGuard 就是把这件定期该做的事自动化，而且每次动手前先问你。

---

## 快速开始

1. 下载 `CCleanGuard.exe`
2. 双击（会弹 UAC —— 因为要删 `Program Files` / `ProgramData` / `Windows` 下的缓存）
3. 点「预览（不删除）」看它打算删什么、能省多少
4. 满意了再点「立即清理」
5. 想要每天自动提醒，点「安装每日提醒」

想卸载：点「移除提醒」，然后删掉 `%LOCALAPPDATA%\CCleanGuard` 整个目录即可。没有注册表残留、没有服务、没有驱动。

---

## 每日提醒是怎么工作的

```
每天 06:00 起，每小时检查一次，到次日 02:00 结束
命中后弹窗问你要不要清理，180 秒无响应自动关闭
点「今天不用」→ 3 小时后再问
每天最多问 3 次，清过一次当天就不再问
```

所以它不会在半夜弹窗，也不会一直骚扰你。

计划任务名：`CCleanGuard-Daily`，最高权限运行，因此**日常运行不会再弹 UAC**。

---

## 它会删什么

### 用户级

| 目标 | 说明 |
|---|---|
| `Chrome\User Data\OptGuideOnDeviceModel` | Chrome 端侧 AI 模型，通常 3~5 GB |
| `Chrome\User Data\ShaderCache` | Chrome 着色器缓存 |
| `Microsoft\vscode-cpptools` | VS Code C++ 符号索引，通常 1~3 GB |
| `Microsoft\WinGet\Packages` | WinGet 下载的安装包 |
| `Microsoft\Office\SolutionPackages` | Office 解决方案包缓存 |
| `NVIDIA\DXCache`、`NVIDIA\GLCache` | 显卡着色器缓存 |
| `AppData\LocalLow\NVIDIA\DXCache` | 同上 |
| `Temp` | 用户临时文件（正在使用的会自动跳过） |
| `npm-cache\_cacache`、`_npx` | npm 缓存 |
| `pip\cache` | pip 缓存 |
| `Steam\htmlcache` | Steam 客户端缓存 |
| `Roaming\Code\*` | VS Code 缓存 / 日志 / 扩展安装包 |

### 管理员级

| 目标 | 说明 |
|---|---|
| `C:\Windows\Temp` | 系统临时文件 |
| `C:\Windows\SoftwareDistribution\Download` | Windows 更新下载缓存 |
| `C:\ProgramData\NVIDIA\NGX` | DLSS 模型缓存 |
| `Google\GoogleUpdater\crx_cache` | Chrome 更新器缓存 |
| `Microsoft Office\Updates` | Office 点击运行的更新包缓存，通常 3 GB+ |

**正在被进程占用的文件会自动跳过**，日志里会标注 `[N MB 仍被占用]`。关掉对应程序后再清理一次就能拿到。

### 明确不碰的东西

- `pagefile.sys` / `swapfile.sys` —— 代码里根本没有这个路径
- `Windows\Installer`、`DriverStore` —— 删了会导致软件卸不掉、驱动挂掉
- 系统还原点、注册表
- 任何用户文档
- 任何不在上表里的路径

---

## 命令行

```powershell
CCleanGuard.exe                       # 打开界面
CCleanGuard.exe --clean               # 直接清理，结束后弹结果
CCleanGuard.exe --clean --dry-run     # 只预览
CCleanGuard.exe --daily               # 每日提醒模式（计划任务用的就是它）
CCleanGuard.exe --install             # 安装每日提醒
CCleanGuard.exe --uninstall           # 移除每日提醒
```

---

## 从源码构建

不需要 Visual Studio、不需要 .NET SDK、不需要联网：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

产物在 `dist\CCleanGuard.exe`。

用的是 Windows 自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。
`/codepage:65001` 是关键：否则 csc 会按系统 ANSI 编码读源码，中文字符串会乱码。

```
ccleanguard/
├── src/
│   ├── Program.cs         入口与命令行分发
│   ├── MainForm.cs        主界面
│   ├── Cleaner.cs         白名单与删除逻辑
│   ├── DailyGuard.cs      每日提醒的窗口/周期判断
│   ├── PromptForm.cs      带倒计时的确认框
│   ├── TaskInstaller.cs   计划任务注册（schtasks + XML）
│   ├── Core.cs            路径、日志、状态
│   ├── AssemblyInfo.cs
│   └── app.manifest       requireAdministrator + DPI aware
├── build.ps1
└── dist/CCleanGuard.exe
```

---

## 常见问题

**双击没反应 / 被杀软拦截？**
EXE 没有代码签名。杀软对「删除文件 + 注册计划任务」这两个行为组合很敏感，误报概率不低。
源码就在仓库里，你可以自己 `build.ps1` 编一个 —— 编译产物和你下载的应该是同一个东西（逐字节可能不同，因为编译时间戳）。

**提示"Windows 已保护你的电脑"？**
SmartScreen 对未知发行者会拦。点「更多信息」→「仍要运行」。介意的话自己编译。

**删了会不会影响正在运行的程序？**
被占用的文件删不掉，会跳过。不会让程序崩溃。已删除的缓存会被重新生成。

**会不会影响虚拟内存？**
不会。`pagefile.sys` 不在白名单里，代码里也没有任何相关逻辑。

**支持 D 盘 / E 盘吗？**
目前只做 C 盘。多盘清理需要每个盘不同的白名单，暂时不在范围内。

**为什么每天最多问 3 次？**
弹窗是为了提醒你，不是骚扰你。3 次没人理就说明今天不需要。

---

## 许可

MIT。随便用、随便改、随便发。
