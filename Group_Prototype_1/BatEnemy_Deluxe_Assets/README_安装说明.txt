DELUXE SPIRAL BAT - Unity Asset Pack
=====================================
安装：
1. 解压 ZIP。
2. 将其中 Assets 文件夹复制到 Unity 项目根目录，合并文件夹。
3. 替换旧版 Assets/Script/obstacles/BatEnemy.cs 与 Assets/Resources/BatEnemy.prefab。
4. 打开 environment_test_scene，点击 Play。原有的自动生成蝙蝠功能依旧有效。

新增：DeluxeBatVisual.cs 负责程序化生成 3D 卡通蝙蝠外观。
- 紫黑绒毛、圆脸、尖耳朵、发光双眼、小獠牙、立体膜翅、骨架翅脉、尾巴
- 翅膀持续拍动，攻击预警时眼睛红光、翼膜偏红
- 不需要外部模型、纹理、材质文件，也不需要额外包
- 螺旋往返、外圈限定、俯冲、1 HP 伤害均沿用原 BatEnemy

说明：资源采用 Unity 运行时构建的程序化网格，不是导入的 FBX 模型。
首次在 Unity Play 时生成精细造型；编辑状态的 prefab 缩略图不会直接显示模型。
尚未在 Unity Editor 内实测。如材质呈粉红色，需确认项目渲染管线与材质 Shader 的兼容性。
