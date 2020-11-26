## Loot Protection
Yet another player loot protection plugin for Rust

Uses ZoneManager, Friends, Clans, Rust teams

Player boxes, workbenches, etc. are protected from opening by others.  With Friends/Clans/Teams support, a player's friends will maintain access.

Note that this is not damage control, only access to contents.


### Commands (for admin)
    - /lp
      - /lp enable/e/1  - Enable plugin
      - /lp disable/d/0 - Disable plugin
      - /lp logging/log/l - Toggle logging
      - /lp status  - Show current config and enable status

### Permissions
    - lootprotect.all - Player overrides all access controls
    - lootprotect.admin - Player can run the /lp command
    - lootprotect.player - Player boxes protected (only if RequirePermission is true

### Configuration
```json
{
  "Options": {
    "RequirePermission": false,
    "useZoneManager": false,
    "useSchedule": false,
    "useRealTime": true,
    "useFriends": false,
    "useClans": false,
    "useTeams": false,
    "HonorRelationships": false,
    "OverrideOven": false,
    "OverrideTC": false,
    "StartEnabled": true,
    "StartLogging": false,
    "LogToFile": false
  },
  "Rules": {
    "bbq.deployed": true,
    "box.wooden.large": true,
    "campfire": true,
    "cursedcauldron.deployed": true,
    "fridge.deployed": true,
    "furnace.large": true,
    "furnace.small": true,
    "mixingtable.deployed": false,
    "player": true,
    "player_corpse": true,
    "recycler_static": false,
    "refinery_small_deployed": false,
    "repairbench_deployed": false,
    "researchtable_deployed": false,
    "woodbox_deployed": true,
    "workbench1.deployed": true,
    "workbench2.deployed": true,
    "workbench3.deployed": true
  },
  "Zones": null,
  "Schedule": "",
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 0
  }
}
```

#### Global Options
    - `useZoneManager` -- Use ZoneManager to only protect boxes in specified zones.
    - `useClans` -- Use various Clans plugins for determining relationships.
    - `useFriends` -- Use various Friends plugins for determining relationships.
    - `useTeams` -- Use Rust native teams for determining relationships.
    - `HonorRelationships` -- If set, honor any of the useXXX features to determine ability to access boxes.
    - `OverrideOven` -- Allow access to ovens (campfire, furnace, etc.).  Set this to only protect storage boxes, etc.
    - `OverrideTC` -- Allow access to authenticate on an unlocked TC.
    - `StartEnabled` -- Start plugin in enabled mode (default true).
    - `StartLogging` -- Log all check activity by defaul on plugin load.
    - `LogToFile` -- Log to dated file in oxide/logs/LootProtect folder.  If false, log to oxide log file/rcon.

#### Rules
    This is a simple list of prefab names and whether or not they will be protected.  Several defaults are included to work with standard storage boxes, furnaces, campfire, etc.
    For each prefab, if true is specified, they will be protected.

#### ZoneManager

    If ZoneManager is loaded, and useZoneManager is true, you can specify zone ids here.  The default value is:

```json
  "Zones": null,
```

    To set a zone or list of zones, specify them as follows:
```json
  "Zones": [
    "123456"
  ],
```
    Or:

```json
  "Zones": [
    "123456",
    "345678"
  ],
```
