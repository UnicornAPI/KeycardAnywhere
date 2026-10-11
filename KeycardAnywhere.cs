using Unicorn.API;
using Unicorn.Events;
using UnityEngine;

namespace KeycardAnywhere
{
    public sealed class Config
    {
        /// <summary>Включить плагин.</summary>
        public bool Enabled = true;

        /// <summary>Работает ли, если игрок в наручниках (в игре закованный не может пользоваться инвентарём).</summary>
        public bool AllowWhenCuffed = false;

        /// <summary>Максимальная дистанция до двери, м (в игре — 15).</summary>
        public float MaxDistance = 15f;

        /// <summary>Писать в лог сервера каждое открытие картой из инвентаря.</summary>
        public bool Debug = false;
    }

    /// <summary>
    /// Двери с уровнем доступа открываются, если подходящая ключ-карта лежит в любом слоте инвентаря —
    /// доставать её в руки не нужно. Если карта уже в руках — работает обычная логика игры.
    /// </summary>
    public sealed class KeycardAnywherePlugin : Plugin
    {
        public override string Name => "KeycardAnywhere";
        public override string Author => "FLX";

        private Config _cfg;

        public override void OnEnabled()
        {
            _cfg = LoadConfig<Config>();
            PlayerEvents.InteractingDoor += OnInteractingDoor;
            Server.PublicInfo["KeycardAnywhere"] = new System.Collections.Generic.Dictionary<string, object>
            {
                ["enabled"] = _cfg.Enabled,
                ["description"] = "Двери с уровнем доступа открываются, если подходящая ключ-карта лежит в любом слоте инвентаря",
                ["works_when_cuffed"] = _cfg.AllowWhenCuffed,
            };
            Log.Info($"[KeycardAnywhere] {(_cfg.Enabled ? "включён" : "выключен в конфиге")}");
        }

        public override void OnDisabled()
        {
            PlayerEvents.InteractingDoor -= OnInteractingDoor;
            Server.PublicInfo.TryRemove("KeycardAnywhere", out _);
        }

        private void OnInteractingDoor(InteractingDoorEventArgs e)
        {
            if (!_cfg.Enabled || !e.IsAllowed) return;
            var door = e.Door;
            var level = door.AccessLevel;
            if (string.IsNullOrEmpty(level)) return;                       // дверь без доступа — открывает игра

            var p = e.Player;
            if (p.IsDead) return;
            if (p.Inventory.CurrentItemHasAccess(level)) return;           // карта в руках — пусть решает игра
            if (p.Team == Team.SCP) return;                                // у SCP своя логика (КПП)
            if (!_cfg.AllowWhenCuffed && p.IsCuffed) return;
            if (!p.Inventory.HasAccess(level, out _, out var itemId)) return; // нет карты — игра откажет сама
            if (Vector3.Distance(p.Position, door.Position) > _cfg.MaxDistance) return;

            e.IsAllowed = false;                                           // обычную проверку игры не запускаем
            bool toggled = door.InteractAllowed();                          // открыть/закрыть как при карте в руках
            if (_cfg.Debug)
                Log.Info($"[KeycardAnywhere] {p.Nickname}: {door.Name} ({level}) картой из инвентаря «{Items.GetName(itemId)}» — {(toggled ? (door.IsOpen ? "открыта" : "закрыта") : "занята/на задержке")}");
        }
    }
}
