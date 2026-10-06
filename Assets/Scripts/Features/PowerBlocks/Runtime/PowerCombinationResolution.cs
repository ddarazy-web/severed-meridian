using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum PowerArea { Point, Plus, Horizontal, Vertical, Blast3, Cross, WideCross, Blast5, Board }
    public enum PowerCombinationKind { RocketRocket, RocketBomb, RocketDrone, BombBomb, BombDrone, DroneDrone, MagnetRocket, MagnetBomb, MagnetDrone, MagnetMagnet }

    public sealed class PowerTransformation
    {
        public BoardCoordinate Coordinate { get; }
        public RabbitColor OriginalColor { get; }
        public RuntimeContent Content { get; }
        public RocketDirection? Direction { get; }
        internal PowerTransformation(RuntimeCell cell, RabbitColor color)
        { Coordinate = cell.Coordinate; OriginalColor = color; Content = cell.Content; Direction = cell.RocketDirection; }
    }

    public sealed class PowerCombination
    {
        public PowerCombinationKind Kind { get; }
        public BoardCoordinate First { get; }
        public BoardCoordinate Center { get; }
        public PowerArea InitialArea { get; }
        public PowerArea DroneArea { get; }
        public int DroneCount { get; }
        public RabbitColor? Color { get; }
        public ReadOnlyCollection<PowerTransformation> Transformations { get; }
        public ReadOnlyCollection<BoardCoordinate> CoveredTargets { get; }
        public bool IsTransformation => Kind >= PowerCombinationKind.MagnetRocket && Kind <= PowerCombinationKind.MagnetDrone;
        internal PowerCombination(PowerCombinationKind kind, BoardCoordinate first, BoardCoordinate center, PowerArea initial,
            PowerArea drone, int count, RabbitColor? color, IEnumerable<PowerTransformation> transformations, IEnumerable<BoardCoordinate> covered)
        { Kind = kind; First = first; Center = center; InitialArea = initial; DroneArea = drone; DroneCount = count; Color = color; Transformations = transformations.ToList().AsReadOnly(); CoveredTargets = covered.ToList().AsReadOnly(); }
    }

    // 조합 재료/범위/변환만 구성한다. 타격·피격 연쇄·낙하는 기존 실행기가 처리한다.
    public static class PowerCombinationResolution
    {
        public static ReadOnlyCollection<BoardCoordinate> Range(LevelRuntimeState state, BoardCoordinate center, PowerArea area)
            => state.Cells.Where(cell => cell.IsActive && (area switch
            {
                PowerArea.Point => cell.Coordinate.Equals(center),
                PowerArea.Plus => Math.Abs(cell.Coordinate.Row - center.Row) + Math.Abs(cell.Coordinate.Column - center.Column) <= 1,
                PowerArea.Horizontal => cell.Coordinate.Row == center.Row,
                PowerArea.Vertical => cell.Coordinate.Column == center.Column,
                PowerArea.Blast3 => Math.Abs(cell.Coordinate.Row - center.Row) <= 1 && Math.Abs(cell.Coordinate.Column - center.Column) <= 1,
                PowerArea.Cross => cell.Coordinate.Row == center.Row || cell.Coordinate.Column == center.Column,
                PowerArea.WideCross => Math.Abs(cell.Coordinate.Row - center.Row) <= 1 || Math.Abs(cell.Coordinate.Column - center.Column) <= 1,
                PowerArea.Blast5 => Math.Abs(cell.Coordinate.Row - center.Row) <= 2 && Math.Abs(cell.Coordinate.Column - center.Column) <= 2,
                _ => true
            })).OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column).Select(c => c.Coordinate).ToList().AsReadOnly();

        // 교환 후 작업 사본만 변경한다. 모든 변환을 마친 뒤 공통 실행기에 발동을 넘긴다.
        internal static PowerCombination Prepare(LevelRuntimeState work, BoardCoordinate first, BoardCoordinate second, TurnEffectContext context)
        {
            RuntimeCell a = work.CellAt(first), b = work.CellAt(second);
            RuntimeContent low = a.Content < b.Content ? a.Content : b.Content, high = a.Content < b.Content ? b.Content : a.Content;
            RocketDirection? direction = a.Content == RuntimeContent.Rocket ? a.RocketDirection : b.RocketDirection;
            PowerCombinationKind kind;
            if (high == RuntimeContent.Magnet) kind = low switch { RuntimeContent.Rocket => PowerCombinationKind.MagnetRocket, RuntimeContent.Bomb => PowerCombinationKind.MagnetBomb, RuntimeContent.Drone => PowerCombinationKind.MagnetDrone, _ => PowerCombinationKind.MagnetMagnet };
            else if (low == RuntimeContent.Rocket) kind = high switch { RuntimeContent.Rocket => PowerCombinationKind.RocketRocket, RuntimeContent.Bomb => PowerCombinationKind.RocketBomb, _ => PowerCombinationKind.RocketDrone };
            else if (low == RuntimeContent.Bomb) kind = high == RuntimeContent.Bomb ? PowerCombinationKind.BombBomb : PowerCombinationKind.BombDrone;
            else kind = PowerCombinationKind.DroneDrone;
            bool transform = kind >= PowerCombinationKind.MagnetRocket && kind <= PowerCombinationKind.MagnetDrone;
            RabbitColor[] colors = work.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && c.Cover != CoverKind.Mold && c.Color.HasValue).Select(c => c.Color.Value).Distinct().OrderBy(c => c).ToArray();
            if (transform && colors.Length == 0) throw new InvalidOperationException("자석 조합이 변환할 일반 블록 없음");
            foreach (RuntimeCell material in new[] { a, b })
            {
                context.RegisterFire(material.Coordinate);
                material.Content = RuntimeContent.Empty; material.Color = null; material.RocketDirection = null;
            }
            RabbitColor? color = transform ? colors[colors.Length == 1 ? 0 : work.Random.Next(colors.Length)] : (RabbitColor?)null;
            List<PowerTransformation> transformations = new List<PowerTransformation>();
            List<BoardCoordinate> covered = new List<BoardCoordinate>();
            if (transform)
                foreach (RuntimeCell cell in work.Cells.Where(c => c.IsActive && c.Content == RuntimeContent.Normal && c.Cover != CoverKind.Mold && c.Color == color).OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column))
                {
                    if (cell.Cover == CoverKind.Web) { covered.Add(cell.Coordinate); continue; }
                    RabbitColor original = cell.Color.Value;
                    DustRules.ConsumeNormal(work, cell, context);
                    cell.Content = low; cell.Color = null;
                    cell.ContentElement = Elements.LegacyElementDefinitions.GetContent(low, work.ElementCatalog);
                    cell.RocketDirection = low == RuntimeContent.Rocket ? (RocketDirection?)work.Random.Next(2) : null;
                    MissionProgressRules.ConsumeColor(work, original, cell.Coordinate);
                    transformations.Add(new PowerTransformation(cell, original));
                }
            PowerArea initial = kind switch
            {
                PowerCombinationKind.RocketRocket => PowerArea.Cross, PowerCombinationKind.RocketBomb => PowerArea.WideCross,
                PowerCombinationKind.BombBomb => PowerArea.Blast5, PowerCombinationKind.DroneDrone => PowerArea.Blast3,
                PowerCombinationKind.MagnetMagnet => PowerArea.Board, _ => PowerArea.Plus
            };
            PowerArea drone = kind == PowerCombinationKind.RocketDrone ? (direction == RocketDirection.Horizontal ? PowerArea.Horizontal : PowerArea.Vertical) :
                kind == PowerCombinationKind.BombDrone ? PowerArea.Blast3 : PowerArea.Point;
            int count = kind == PowerCombinationKind.DroneDrone ? 3 : kind == PowerCombinationKind.RocketDrone || kind == PowerCombinationKind.BombDrone ? 1 : 0;
            return new PowerCombination(kind, first, second, initial, drone, count, color, transformations, covered);
        }
    }
}
