# TestHybridCLR

## HybridCLR

项目初始化教程：[快速上手](https://hybridclr.doc.code-philosophy.com/docs/beginner/quickstart)

git添加子模块

```bash
git submodule add https://github.com/focus-creative-games/hybridclr.git HybridCLRData/hybridclr_repo
git submodule add https://github.com/focus-creative-games/il2cpp_plus.git HybridCLRData/il2cpp_plus_repo
```

git更新子模块

```bash
git submodule update --init --recursive
```

## 打包流程
- 运行菜单 `HybridCLR/Generate/All` 一键执行必要的生成操作
- 将`HybridCLRData/HotUpdateDlls`下的热更新dll添加到项目的热更新资源管理系统
- 将`HybridCLRData/AssembliesPostIl2CppStrip`下的补充元数据 dll添加到项目的热更新资源管理系统
- 根据你项目原来的打包流程打包
