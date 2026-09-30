using Board;
using Levels;
using Simulation;

namespace GameScreen
{
    public static class PuzzleArtworkPaths
    {
        private static readonly string[] colors = { "pink", "yellow", "blue", "green", "purple" };
        public const string Floor = "BoardTerrain/Floor/floor-center-v1-256";
        public const string Arrival = "BoardDevices/Recovery/recovery-exit-v1-256";
        public const string Recovery = "BoardDevices/Recovery/recovery-part-v1-256";
        public static string Rabbit(RabbitColor color) => "Blocks/rabbit-" + colors[(int)color] + "-v1-256";
        public static string Power(InitialBlockKind kind, RocketDirection direction) => kind switch
        {
            InitialBlockKind.Rocket => "PowerBlocks/cleaning-rocket-" + (direction == RocketDirection.Horizontal ? "horizontal" : "vertical") + "-v1",
            InitialBlockKind.Bomb => "PowerBlocks/moon-bomb-v1",
            InitialBlockKind.Drone => "PowerBlocks/collection-drone-v1",
            InitialBlockKind.Magnet => "PowerBlocks/rainbow-magnet-v1",
            _ => null
        };
        public static string Content(RuntimeCell cell)
        {
            if (!cell.IsActive || cell.Cover == CoverKind.Mold) return null;
            return cell.Content switch
            {
                RuntimeContent.Normal => cell.Color.HasValue ? Rabbit(cell.Color.Value) : null,
                RuntimeContent.Rocket => Power(InitialBlockKind.Rocket, cell.RocketDirection ?? RocketDirection.Horizontal),
                RuntimeContent.Bomb => Power(InitialBlockKind.Bomb, RocketDirection.Horizontal),
                RuntimeContent.Drone => Power(InitialBlockKind.Drone, RocketDirection.Horizontal),
                RuntimeContent.Magnet => Power(InitialBlockKind.Magnet, RocketDirection.Horizontal),
                RuntimeContent.Recovery => Recovery,
                _ => null
            };
        }
        public static string Obstacle(RuntimeObstacle body)
        {
            if (body.Durability <= 0 && body.Definition.Kind != ObstacleKind.Generator) return null;
            string suffix = "-durability-" + body.Durability + "-v1-256";
            return body.Definition.Kind switch
            {
                ObstacleKind.Crate => "Obstacles/Crate/crate" + suffix,
                ObstacleKind.Scrap => "Obstacles/Scrap/scrap" + suffix,
                ObstacleKind.Safe => "Obstacles/RecoveryCapsule/recovery-capsule" + suffix,
                ObstacleKind.ColorLock => "Obstacles/ColorLock/color-lock-" + colors[(int)body.Definition.Color] + suffix,
                ObstacleKind.Appliance => "Obstacles/MetalRodBox/metal-rod-box-" + colors[(int)body.Definition.Color] + suffix,
                ObstacleKind.Generator => "Obstacles/Generator/generator-charge-" + body.Charge + "-of-" + body.Definition.RequiredCharge + "-v1-512",
                _ => null
            };
        }
        public static string Cover(RuntimeCell cell) => !cell.Cover.HasValue ? null : cell.Cover == CoverKind.Mold
            ? "Obstacles/Mold/mold-base-v1-256" : "Obstacles/Web/web-durability-" + cell.CoverDurability + "-v2-256";
        public static string Dust(int durability) => durability > 0 ? "Obstacles/Dust/dust-durability-" + durability + "-v1-256" : null;
        public static string Wall(bool vertical) => "BoardTerrain/Walls/scrap-wall-" + (vertical ? "vertical" : "horizontal") + "-v1-256";
        public static string Wire(bool vertical) => "BoardDevices/Wiring/wire-" + (vertical ? "vertical" : "horizontal") + "-v1-256";
        public static string Portal(int index, bool exit)
        {
            string shape = (index % 4) switch { 0 => "cyan-circle", 1 => "orange-triangle", 2 => "purple-diamond", _ => "green-plus" };
            return "BoardDevices/Portals/portal-" + shape + (exit ? "-exit" : "-entry") + "-v1-256";
        }
        public static string Terminal(int slot, bool connected)
        {
            string shape = (slot % 3) switch { 0 => "amber-triangle", 1 => "cyan-circle", _ => "purple-diamond" };
            return "BoardDevices/Wiring/terminal-" + shape + (connected ? "-on" : "-off") + "-v1-256";
        }
    }
}
