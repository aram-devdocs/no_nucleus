# Nucleus Dynamic Warfare

**EARLY ALPHA - in active development. Expect bugs, breakage, and changes between versions.**

Nucleus is a fly-and-command mod for [Nuclear Option](https://store.steampowered.com/app/2168680/).
You stay in the cockpit and fly, while issuing orders to an AI-driven dynamic battlespace
around you. It turns a single sortie into a living front line: friendly and hostile forces
pursue objectives, react to the situation, and fight on without you having to micromanage
every unit.

## What's in the box

Nucleus ships as a set of BepInEx plugins:

- **Nucleus.Platform** - the shared runtime that the other plugins load on top of.
- **Nucleus.Commander** - the in-cockpit command layer (issue and track orders).
- **Nucleus.Warfare** - the dynamic warfare / battlespace simulation.
- **Nucleus.Squad** - squad and formation behaviour.
- **Nucleus.Build** - supporting tooling.

## Status

This is an **early alpha**. The feature set, command UI, and behaviour are still moving.
It is published here so it can be installed and play-tested easily - it is **not** a finished,
stable release. Please treat crashes and oddities as expected and report them.

## Requirements

- Nuclear Option (Steam AppId `2168680`)
- BepInEx 5 (installed automatically as a dependency: `BepInEx-BepInExPack-5.4.2100`)

## Install

### With a mod manager (recommended)

Use [r2modman](https://thunderstore.io/c/nuclear-option/p/ebkr/r2modman/) or the
Thunderstore Mod Manager:

1. Select the **Nuclear Option** game.
2. Search for **NucleusDynamicWarfare** and install it.
3. The BepInEx dependency is pulled in automatically.
4. Launch the game **through the mod manager** ("Start modded").

### Manual install

1. Install [BepInEx 5 (BepInExPack)](https://thunderstore.io/c/nuclear-option/) into your
   Nuclear Option folder and run the game once so it generates its folders.
2. Extract this package and copy the contents of its `plugins/` folder into
   `<Nuclear Option>/BepInEx/plugins/` so you end up with:
   - `BepInEx/plugins/Nucleus.Platform/`
   - `BepInEx/plugins/Nucleus.Commander/`
   - `BepInEx/plugins/Nucleus.Warfare/`
   - `BepInEx/plugins/Nucleus.Squad/`
   - `BepInEx/plugins/Nucleus.Build/`
3. Launch the game.

## Source

Built from [github.com/aram-devdocs/no_nucleus](https://github.com/aram-devdocs/no_nucleus).
