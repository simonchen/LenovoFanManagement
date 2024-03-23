# LenovoFanManagement （中文版）

Windows Desktop App. to manage Lenovo Fan RPM special for Thinkpad E16 gen 1 
OS: Windows 10 / 11
Platform: x64

This project is based on [ec_tpfan](https://github.com/simonchen/ec_tpfan)

## 允许测试签名 (Allowing to test signing ON）
LenovoFan.exe需要加载驱动程序，为防上不能正常加载，请执行下命命令，允许测试签名：
```
bcdedit -set TESTSIGNING ON
```
**注意：** BitLock如果打开，重启完重启后，将会问密钥进入！（**请预先备份BitLock密钥到u盘**）

## 系统启动时运行
LenovoFan.exe 需要获取管理员权限才能正常运行，建议执行下面UAC命令，调整为“从不通知”，
可以避免每次系统启动时提示需要管理员权限运行。
···
C:\Windows\System32\UserAccountControlSettings.exe
···

# Workflow build
You can manually build the solution by implementing actions,
Note: [How to set write permission](https://stackoverflow.com/questions/70435286/resource-not-accessible-by-integration-on-github-post-repos-owner-repo-ac)
```
Go to repository "Settings".
After that it will show you a left pane where you will find "Actions"
Expand "Actions" tab
Click on "General" under options tab.
Now on new page scroll down and you will fine "Workflow Permissions"
Select "Read and Write" under "Workflow Permissions".
```
