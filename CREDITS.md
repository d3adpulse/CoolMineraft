# Ассеты и лицензии

## Текстуры блоков (`Assets/Textures/block_atlas.png`)

Атлас 4×4 (64×64 px, тайлы 16×16) собран из текстур **Minetest Game**:

- `default_stone.png`
- `default_dirt.png`
- `default_grass.png`
- `default_sand.png`
- `default_water.png`

Источник: <https://github.com/minetest/minetest_game> (`mods/default/textures/`).

Лицензия медиа Minetest Game: **CC BY-SA 3.0**
(<https://creativecommons.org/licenses/by-sa/3.0/>),
copyright (C) 2010–2012 celeron55, Perttu Ahola и участники проекта.

Атлас (раскладка тайлов) создан для этого проекта и распространяется на тех же
условиях CC BY-SA 3.0.

> Если нужна лицензия без Share-Alike — замените `block_atlas.png` на свои
> текстуры (например CC0 от Kenney) и поправьте индексы тайлов
> в `Scripts/World/BlockDatabase.cs`.

## Модели (`Assets/Models/*.glb`)

`character-a.glb`, `character-m.glb` — из набора **Blocky Characters**
(<https://kenney.nl/assets/blocky-characters>), автор **Kenney**,
лицензия **CC0 1.0** (public domain).

Модели в проект не инстанцируются: это примеры для проверки импорта glTF 2.0
(этап 8). Их можно перетащить в сцену в редакторе.

## Код

Код проекта написан для этого проекта. В качестве справки по подходам
(структура чанка, greedy meshing, работа с `FastNoiseLite`) использовался
открытый проект <https://github.com/devkine/godot-csharp-voxel-greedy>
(проверка порядка обхода вершин и схемы `ArrayMesh`).
