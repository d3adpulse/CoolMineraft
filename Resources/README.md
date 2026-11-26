# Resources

Описания блоков (`BlockDef`: тайлы атласа, `opaque`, `solid`, `liquid`, твёрдость)
создаются в `Scripts/World/BlockDatabase.cs` в методе `BuildDefaultDefinitions()`.

Так сделано намеренно:

- не нужно вручную собирать `.tres` с массивом подресурсов;
- таблица доступна до первого кадра и читается из фоновых потоков;
- добавить блок = добавить `BlockId` + одну запись `Set(...)`.

Если позже понадобится редактировать блоки в инспекторе, схема расширяется так:

1. `BlockDatabaseResource : Resource` с `[Export] BlockDef[] Definitions`.
2. `.tres` с этим ресурсом кладётся сюда.
3. `BlockDatabase._Ready()` грузит `.tres`, а если его нет — строит значения по умолчанию.
