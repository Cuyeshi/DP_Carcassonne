using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Core_logic
{
    public class TileData
    {
        public string Id { get; private set; }
        public int Rotation { get; private set; }

        // Все регионы внутри этого тайла (нужно для миплов и подсчета очков)
        public List<TileRegion> Regions { get; private set; }

        // Грани (0-Север, 1-Восток, 2-Юг, 3-Запад)
        private TileEdge[] _baseEdges;

        // Центральный регион (нужно для монастырей или перекрестков)
        public TileRegion CenterRegion { get; private set; }


        public TileData(string id, List<TileRegion> regions, TileEdge[] edges, TileRegion center = null)
        {
            Id = id;
            Regions = regions;
            _baseEdges = edges;
            CenterRegion = center;
            Rotation = 0;

            // НОВОЕ: Назначаем себя родителем для всех своих регионов
            foreach (var region in Regions)
            {
                region.ParentTile = this;
            }
        }

        public void RotateRight()
        {
            Rotation = (Rotation + 1) % 4;
        }

        public TileEdge GetEdge(Direction dir)
        {
            int originalIndex = ((int)dir - Rotation + 4) % 4;
            return _baseEdges[originalIndex];
        }

        /// <summary>
        /// Принудительно установить поворот (0, 1, 2, 3)
        /// </summary>
        public void SetRotation(int rotation)
        {
            // Защита от ошибок: гарантируем, что значение всегда от 0 до 3
            Rotation = (rotation % 4 + 4) % 4;
        }

        /// <summary>
        /// Глубокое копирование тайла для симуляций ИИ
        /// </summary>
        public TileData Clone()
        {
            // 1. Копируем все регионы и запоминаем связь "Старый -> Новый"
            Dictionary<TileRegion, TileRegion> regionMap = new Dictionary<TileRegion, TileRegion>();
            List<TileRegion> newRegions = new List<TileRegion>();

            foreach (var r in Regions)
            {
                TileRegion newRegion = new TileRegion(r.Id, r.Type, r.OpenEdges, r.HasShield, r.IsCloister, r.IsPlaceable);
                regionMap[r] = newRegion;
                newRegions.Add(newRegion);
            }

            // ИСПРАВЛЕНИЕ: Восстанавливаем внутренние связи (AdjacentRegions) в клоне!
            foreach (var oldRegion in Regions)
            {
                foreach (var oldAdj in oldRegion.AdjacentRegions)
                {
                    regionMap[oldRegion].AdjacentRegions.Add(regionMap[oldAdj]);
                }
            }

            // 2. Собираем новые грани, используя ТОЛЬКО новые регионы
            TileEdge[] newEdges = new TileEdge[4];
            for (int i = 0; i < 4; i++)
            {
                TileEdge oldEdge = _baseEdges[i];
                newEdges[i] = new TileEdge(
                    regionMap[oldEdge.Left],
                    regionMap[oldEdge.Center],
                    regionMap[oldEdge.Right]
                );
            }

            // 3. Центральный регион (если есть)
            TileRegion newCenter = CenterRegion != null ? regionMap[CenterRegion] : null;

            // 4. Создаем новый тайл. (Конструктор сам пропишет ParentTile для новых регионов!)
            TileData clonedTile = new TileData(this.Id, newRegions, newEdges, newCenter);
            clonedTile.SetRotation(this.Rotation);

            return clonedTile;
        }
    }
}
