# Assets/Materials

Материал чанков создаётся кодом в `Scripts/World/BlockDatabase.cs`
(`BuildMaterial()`): `StandardMaterial3D` с атласом, `Nearest`-фильтрацией
и `VertexColorUseAsAlbedo = true`.

Отдельный `.tres` здесь не нужен: один материал на все чанки — это меньше
draw call'ов и проще менять настройки в одном месте.
