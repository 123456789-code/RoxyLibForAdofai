@echo off
echo 正在复制 RoxyLib（排除 *.pdb）...
robocopy "E:\code\RoxyLibForAdofai\RoxyLib\bin\Debug\net481" "D:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\Mods\RoxyLib" /E /XF *.pdb /IS

echo.
echo 正在复制 RoxyExample（排除 *.pdb）...
robocopy "E:\code\RoxyLibForAdofai\RoxyExample\bin\Debug\net481" "D:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\Mods\RoxyExample" /E /XF *.pdb /IS

echo.
echo 复制完成！
pause