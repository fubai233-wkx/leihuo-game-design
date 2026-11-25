# 2D Platform Controller

类《蔚蓝》（Celeste）手感的 2D 平台跳跃控制器，Unity 工程。
The controller follows Celeste.

## 环境要求

| 项 | 值 |
|---|---|
| Unity 版本 | **2021.3.15f1**（务必使用同一版本，不要用更高版本打开） |
| 平台 | Windows / macOS |
| 额外依赖 | 无（DOTween 已内置于 `Assets/ProPlatformer/Plugins`） |

> 用更高版本的 Unity 打开会触发工程升级，之后队友再用 2021.3 就打不开了。请勿升级。

## 如何运行

1. 安装 Unity Hub，并在 Hub 里安装 **2021.3.15f1**；
2. Hub → `Add project from disk`，选择本仓库根目录（含 `Assets`、`ProjectSettings` 的那一层）；
3. 首次打开会自动重建 `Library/`，需要几分钟；
4. 打开 `Assets/ProPlatformer/Scenes/Main.unity`，点编辑器顶部的 ▶ Play。

## 操作

| 动作 | 按键 |
|---|---|
| 左右移动 | `A` / `D` 或 `←` / `→` |
| 跳跃 | `Space`（支持长按跳高、土狼时间、跳跃缓冲） |
| 冲刺 | `K` |
| 抓墙 / 攀爬 | `J` |

## 目录结构

```
Assets/
└─ ProPlatformer/
   ├─ Scenes/Main.unity          唯一场景，游戏入口
   ├─ _Scripts/                  全部 C# 代码
   │  ├─ Core/                   主角控制器、状态机（Normal / Dash / Climb）
   │  ├─ Components/             落地检测、蹬墙跳
   │  ├─ Configs/PlayerParams    手感参数定义
   │  ├─ Effects/  PostProcess/  拖尾、波纹、屏幕特效
   │  └─ Level/  EnemyBlock.cs  ArenaBoss.cs  关卡、敌人、Boss
   ├─ _Prefabs/                  主角渲染器、相机、特效预制体
   ├─ Sprites/  _Arts/           美术资源（材质、Shader、纹理）
   ├─ Resources/                 运行时按名加载的资源
   ├─ Plugins/Demigiant/DOTween  第三方补间动画库
   └─ PlayerParam.asset          手感参数（Inspector 里可直接调）
ProjectSettings/                 项目设置（分辨率、物理、输入、版本）
Packages/                        依赖包清单
```

## 调手感

选中场景里的 Player，在 Inspector 中可编辑 `PlayerParam`（对应 `Assets/ProPlatformer/PlayerParam.asset`），常用字段：

- `JumpSpeed` 跳跃初速度、`VarJumpTime` 可变跳跃时长、`Gravity` 重力
- `DashSpeed` 冲刺速度、`DashTime` 冲刺时长、`MaxDashes` 冲刺次数
- `ClimbUpSpeed` / `ClimbDownSpeed` 攀爬速度
- `EnableWallSlide` / `EnableWallBoost` / `EnableJumpGrace` 各项辅助开关

## 提交规范（重要）

- **不要提交** `Library/`、`Temp/`、`Logs/`、`obj/`、`UserSettings/`、`.vs/`、`*.csproj`、`*.sln` —— 这些已在本仓库的 `.gitignore` 中忽略；
- 每个 `.meta` 文件必须跟着对应资源一起提交，否则引用会丢；
- 多人协作请开分支（`feature/xxx`），通过 PR 合并到 `main`；
- 单个二进制资源超过 50 MB 时，改用 Git LFS。

## 来源与授权

> TODO（请团队补充）：如本项目基于开源实现二次开发，请在此注明来源仓库与许可证；
> 注意：本仓库当前为 **public**，公开前请确认代码来源与授权允许公开。
