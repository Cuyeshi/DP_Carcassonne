using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core_logic
{
    public class DeckManager
    {
        private System.Random _rng = new System.Random();
        private List<TileData> _deck = new List<TileData>();

        public DeckManager()
        {
            GenerateStandardDeck();
            ShuffleDeck();
        }

        public TileData DrawTile()
        {
            if (_deck.Count == 0) return null;
            TileData drawnTile = _deck[0];
            _deck.RemoveAt(0);
            return drawnTile;
        }

        public int RemainingTiles => _deck.Count;

        public List<TileData> GetDeckCopy()
        {
            List<TileData> copy = new List<TileData>();
            foreach (var t in _deck) copy.Add(t.Clone());
            return copy;
        }

        private void ShuffleDeck()
        {
            for (int i = 0; i < _deck.Count; i++)
            {
                TileData temp = _deck[i];
                int randomIndex = _rng.Next(i, _deck.Count);
                _deck[i] = _deck[randomIndex];
                _deck[randomIndex] = temp;
            }
        }

        // ==========================================
        // ГЕНЕРАЦИЯ ОРИГИНАЛЬНОЙ КОЛОДЫ КАРКАССОНА (72 тайла)
        // ==========================================
        private void GenerateStandardDeck()
        {
            // 1. Монастыри
            for (int i = 0; i < 4; i++) _deck.Add(CreateTile1_Monastery($"Tile1_Monastery_{i}"));
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile2_MonasteryRoad($"Tile2_MonasteryRoad_{i}"));

            // 3-4. Полный город
            for (int i = 0; i < 1; i++) _deck.Add(CreateTile3_FullCity($"Tile3_FullCityShield_{i}", true));
            // Место для добавления тайла с центром города без щита "CreateTile4".

            // 5-8. Город на 3 грани
            for (int i = 0; i < 1; i++) _deck.Add(CreateTile5_City3_Field($"Tile5_City3Shield_{i}", true));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile6_City3_Field($"Tile6_City3_{i}", false));
            for (int i = 0; i < 1; i++) _deck.Add(CreateTile7_City3_Road($"Tile7_City3RoadShield_{i}", true));
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile8_City3_Road($"Tile8_City3Road_{i}", false));

            // 9-12. Город углом (2 соседние грани)
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile9_CityCorner_Field($"Tile9_CornerShield_{i}", true));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile10_CityCorner_Field($"Tile10_Corner_{i}", false));
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile11_CityCorner_Road($"Tile11_CornerRoadShield_{i}", true));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile12_CityCorner_Road($"Tile12_CornerRoad_{i}", false));

            // 13-14. Город труба (2 противоположные грани)
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile13_CityTube($"Tile13_TubeShield_{i}", true));
            for (int i = 0; i < 1; i++) _deck.Add(CreateTile14_CityTube($"Tile14_Tube_{i}", false));

            // 15-16. Два раздельных города
            for (int i = 0; i < 2; i++) _deck.Add(CreateTile15_TwoCitiesAdj($"Tile15_TwoCitiesAdj_{i}"));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile16_TwoCitiesOpp($"Tile16_TwoCitiesOpp_{i}"));

            // 17-21. Город-шапочка (1 грань)
            for (int i = 0; i < 5; i++) _deck.Add(CreateTile17_CityCap($"Tile17_CityCap_{i}"));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile18_CityCap_CurveWS($"Tile18_CapCurveWS_{i}")); // Дорога Запад-Юг
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile19_CityCap_CurveSE($"Tile19_CapCurveSE_{i}")); // Дорога Юг-Восток
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile20_CityCap_Cross3($"Tile20_CapCross3_{i}"));
            for (int i = 0; i < 3; i++) _deck.Add(CreateTile21_CityCap_Straight($"Tile21_CapStraight_{i}")); // Плюс 1 стартовый тайл создается отдельно

            // 22-25. Дороги без городов
            for (int i = 0; i < 8; i++) _deck.Add(CreateTile22_StraightRoad($"Tile22_StraightRoad_{i}"));
            for (int i = 0; i < 9; i++) _deck.Add(CreateTile23_RoadTurn($"Tile23_RoadTurn_{i}"));
            for (int i = 0; i < 4; i++) _deck.Add(CreateTile24_Cross3($"Tile24_Cross3_{i}"));
            for (int i = 0; i < 1; i++) _deck.Add(CreateTile25_Cross4($"Tile25_Cross4_{i}"));
        }

        // ==========================================
        // ФАБРИКИ (25 ТИПОВ ТАЙЛОВ)
        // ==========================================

        public TileData CreateClassicStartTile(string id = "StartTile")
        {
            return CreateTile21_CityCap_Straight(id); // Классический стартовый тайл - это Шапочка с прямой дорогой!
        }

        private TileData CreateTile1_Monastery(string id)
        {
            TileRegion cloister = new TileRegion(0, TerrainType.Monastery, 0, false, true, true);
            TileRegion field = new TileRegion(1, TerrainType.Field, 0);
            cloister.AddAdjacent(field);
            TileEdge e = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { cloister, field }, new[] { e, e, e, e }, cloister);
        }

        private TileData CreateTile2_MonasteryRoad(string id)
        {
            TileRegion cloister = new TileRegion(0, TerrainType.Monastery, 0, false, true, true);
            TileRegion road = new TileRegion(1, TerrainType.Road, 1);
            TileRegion field = new TileRegion(2, TerrainType.Field, 0); // Единое поле, огибающее дорогу

            cloister.AddAdjacent(field); cloister.AddAdjacent(road);
            road.AddAdjacent(field);

            TileEdge fEdge = new TileEdge(field, field, field);
            TileEdge sEdge = new TileEdge(field, road, field);
            return new TileData(id, new List<TileRegion> { cloister, road, field }, new[] { fEdge, fEdge, sEdge, fEdge }, cloister);
        }

        private TileData CreateTile3_FullCity(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 4, hasShield);
            TileEdge e = new TileEdge(city, city, city);
            return new TileData(id, new List<TileRegion> { city }, new[] { e, e, e, e });
        }

        // 5-6. Город на 3 стороны, поле на юге
        private TileData CreateTile5_City3_Field(string id, bool hasShield) { return CreateTile6_City3_Field(id, hasShield); }
        private TileData CreateTile6_City3_Field(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 3, hasShield);
            TileRegion field = new TileRegion(1, TerrainType.Field, 1);
            city.AddAdjacent(field);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge fEdge = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { city, field }, new[] { cEdge, cEdge, fEdge, cEdge });
        }

        // 7-8. Город на 3 стороны, дорога на юге (дорога упирается в город)
        private TileData CreateTile7_City3_Road(string id, bool hasShield) { return CreateTile8_City3_Road(id, hasShield); }
        private TileData CreateTile8_City3_Road(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 3, hasShield);
            TileRegion road = new TileRegion(1, TerrainType.Road, 1);
            TileRegion fieldSW = new TileRegion(2, TerrainType.Field, 0);
            TileRegion fieldSE = new TileRegion(3, TerrainType.Field, 0);

            city.AddAdjacent(road); city.AddAdjacent(fieldSW); city.AddAdjacent(fieldSE);
            road.AddAdjacent(fieldSW); road.AddAdjacent(fieldSE);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge sEdge = new TileEdge(fieldSE, road, fieldSW); // Смотрим из центра на Юг
            return new TileData(id, new List<TileRegion> { city, road, fieldSW, fieldSE }, new[] { cEdge, cEdge, sEdge, cEdge });
        }

        // 9-10. Город углом (Север, Запад), поле
        private TileData CreateTile9_CityCorner_Field(string id, bool hasShield) { return CreateTile10_CityCorner_Field(id, hasShield); }
        private TileData CreateTile10_CityCorner_Field(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 2, hasShield);
            TileRegion field = new TileRegion(1, TerrainType.Field, 2);
            city.AddAdjacent(field);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge fEdge = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { city, field }, new[] { cEdge, fEdge, fEdge, cEdge });
        }

        // 11-12. Город углом (Север, Запад), дорога огибает город (Юг -> Восток)
        private TileData CreateTile11_CityCorner_Road(string id, bool hasShield) { return CreateTile12_CityCorner_Road(id, hasShield); }
        private TileData CreateTile12_CityCorner_Road(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 2, hasShield);
            TileRegion road = new TileRegion(1, TerrainType.Road, 2);
            TileRegion fieldInner = new TileRegion(2, TerrainType.Field, 0); // Между городом и дорогой
            TileRegion fieldOuter = new TileRegion(3, TerrainType.Field, 0); // За дорогой

            city.AddAdjacent(fieldInner);
            road.AddAdjacent(fieldInner); road.AddAdjacent(fieldOuter);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge eEdge = new TileEdge(fieldInner, road, fieldOuter);
            TileEdge sEdge = new TileEdge(fieldOuter, road, fieldInner);
            return new TileData(id, new List<TileRegion> { city, road, fieldInner, fieldOuter }, new[] { cEdge, eEdge, sEdge, cEdge });
        }

        // 13-14. Город труба
        private TileData CreateTile13_CityTube(string id, bool hasShield) { return CreateTile14_CityTube(id, hasShield); }
        private TileData CreateTile14_CityTube(string id, bool hasShield)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 2, hasShield);
            TileRegion fieldN = new TileRegion(1, TerrainType.Field, 1);
            TileRegion fieldS = new TileRegion(2, TerrainType.Field, 1);

            city.AddAdjacent(fieldN); city.AddAdjacent(fieldS);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge fnEdge = new TileEdge(fieldN, fieldN, fieldN);
            TileEdge fsEdge = new TileEdge(fieldS, fieldS, fieldS);
            return new TileData(id, new List<TileRegion> { city, fieldN, fieldS }, new[] { fnEdge, cEdge, fsEdge, cEdge });
        }

        // 15. Два раздельных города рядом (Север, Запад)
        private TileData CreateTile15_TwoCitiesAdj(string id)
        {
            TileRegion cityN = new TileRegion(0, TerrainType.City, 1);
            TileRegion cityW = new TileRegion(1, TerrainType.City, 1);
            TileRegion field = new TileRegion(2, TerrainType.Field, 2);

            cityN.AddAdjacent(field); cityW.AddAdjacent(field);

            TileEdge cnEdge = new TileEdge(cityN, cityN, cityN);
            TileEdge cwEdge = new TileEdge(cityW, cityW, cityW);
            TileEdge fEdge = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { cityN, cityW, field }, new[] { cnEdge, fEdge, fEdge, cwEdge });
        }

        // 16. Два раздельных города напротив (Север, Юг)
        private TileData CreateTile16_TwoCitiesOpp(string id)
        {
            TileRegion cityN = new TileRegion(0, TerrainType.City, 1);
            TileRegion cityS = new TileRegion(1, TerrainType.City, 1);
            TileRegion field = new TileRegion(2, TerrainType.Field, 2);

            cityN.AddAdjacent(field); cityS.AddAdjacent(field);

            TileEdge cnEdge = new TileEdge(cityN, cityN, cityN);
            TileEdge csEdge = new TileEdge(cityS, cityS, cityS);
            TileEdge fEdge = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { cityN, cityS, field }, new[] { cnEdge, fEdge, csEdge, fEdge });
        }

        // 17. Город шапочка (Север)
        private TileData CreateTile17_CityCap(string id)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 1);
            TileRegion field = new TileRegion(1, TerrainType.Field, 3);
            city.AddAdjacent(field);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge fEdge = new TileEdge(field, field, field);
            return new TileData(id, new List<TileRegion> { city, field }, new[] { cEdge, fEdge, fEdge, fEdge });
        }

        // 18. Шапочка (Север), Дорога Запад-Юг
        private TileData CreateTile18_CityCap_CurveWS(string id)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 1);
            TileRegion road = new TileRegion(1, TerrainType.Road, 2);
            TileRegion fieldInner = new TileRegion(2, TerrainType.Field, 0); // Юго-Запад
            TileRegion fieldOuter = new TileRegion(3, TerrainType.Field, 0); // Огибает город и дорогу

            city.AddAdjacent(fieldOuter);
            road.AddAdjacent(fieldInner); road.AddAdjacent(fieldOuter);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge foEdge = new TileEdge(fieldOuter, fieldOuter, fieldOuter);
            TileEdge sEdge = new TileEdge(fieldOuter, road, fieldInner);
            TileEdge wEdge = new TileEdge(fieldInner, road, fieldOuter);

            return new TileData(id, new List<TileRegion> { city, road, fieldInner, fieldOuter }, new[] { cEdge, foEdge, sEdge, wEdge });
        }

        // 19. Шапочка (Север), Дорога Юг-Восток
        private TileData CreateTile19_CityCap_CurveSE(string id)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 1);
            TileRegion road = new TileRegion(1, TerrainType.Road, 2);
            TileRegion fieldInner = new TileRegion(2, TerrainType.Field, 0); // Юго-Восток
            TileRegion fieldOuter = new TileRegion(3, TerrainType.Field, 0);

            city.AddAdjacent(fieldOuter);
            road.AddAdjacent(fieldInner); road.AddAdjacent(fieldOuter);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge eEdge = new TileEdge(fieldInner, road, fieldOuter);
            TileEdge sEdge = new TileEdge(fieldOuter, road, fieldInner);
            TileEdge foEdge = new TileEdge(fieldOuter, fieldOuter, fieldOuter);

            return new TileData(id, new List<TileRegion> { city, road, fieldInner, fieldOuter }, new[] { cEdge, eEdge, sEdge, foEdge });
        }

        // 20. Шапочка (Север), Перекресток (Запад, Юг, Восток)
        private TileData CreateTile20_CityCap_Cross3(string id)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 1);
            TileRegion roadE = new TileRegion(1, TerrainType.Road, 1);
            TileRegion roadS = new TileRegion(2, TerrainType.Road, 1);
            TileRegion roadW = new TileRegion(3, TerrainType.Road, 1);
            TileRegion center = new TileRegion(4, TerrainType.Road, 0, false, false, false);

            // Одно единое поле на Севере (огибает город)
            TileRegion fNorth = new TileRegion(5, TerrainType.Field, 0);
            TileRegion fSE = new TileRegion(6, TerrainType.Field, 0);
            TileRegion fSW = new TileRegion(7, TerrainType.Field, 0);

            // Настройка связей:
            city.AddAdjacent(fNorth); // Город лежит на этом огромном поле

            center.AddAdjacent(roadE); center.AddAdjacent(roadS); center.AddAdjacent(roadW);

            roadE.AddAdjacent(fNorth); roadE.AddAdjacent(fSE); // Восточная дорога касается северного и юг-вост. поля
            roadS.AddAdjacent(fSE); roadS.AddAdjacent(fSW);
            roadW.AddAdjacent(fSW); roadW.AddAdjacent(fNorth); // Западная дорога касается юг-зап. и северного поля

            // Собираем грани
            TileEdge cEdge = new TileEdge(city, city, city); // Север
            TileEdge eEdge = new TileEdge(fNorth, roadE, fSE); // Восток (Смотрим из центра: слева Северное поле, справа Юго-Восточное)
            TileEdge sEdge = new TileEdge(fSE, roadS, fSW);    // Юг
            TileEdge wEdge = new TileEdge(fSW, roadW, fNorth); // Запад (Смотрим из центра: слева Юго-Западное, справа Северное)

            return new TileData(id, new List<TileRegion> { city, roadE, roadS, roadW, center, fNorth, fSE, fSW }, new[] { cEdge, eEdge, sEdge, wEdge }, center);
        }

        // 21. Шапочка (Север), Прямая дорога (Запад-Восток)
        private TileData CreateTile21_CityCap_Straight(string id)
        {
            TileRegion city = new TileRegion(0, TerrainType.City, 1);
            TileRegion road = new TileRegion(1, TerrainType.Road, 2);
            TileRegion fNorth = new TileRegion(2, TerrainType.Field, 0); // Поле между городом и дорогой
            TileRegion fSouth = new TileRegion(3, TerrainType.Field, 0); // Поле под дорогой

            city.AddAdjacent(fNorth);
            road.AddAdjacent(fNorth); road.AddAdjacent(fSouth);

            TileEdge cEdge = new TileEdge(city, city, city);
            TileEdge eEdge = new TileEdge(fNorth, road, fSouth);
            TileEdge sEdge = new TileEdge(fSouth, fSouth, fSouth);
            TileEdge wEdge = new TileEdge(fSouth, road, fNorth);

            return new TileData(id, new List<TileRegion> { city, road, fNorth, fSouth }, new[] { cEdge, eEdge, sEdge, wEdge });
        }

        // 22. Прямая дорога
        private TileData CreateTile22_StraightRoad(string id)
        {
            TileRegion road = new TileRegion(0, TerrainType.Road, 2);
            TileRegion fLeft = new TileRegion(1, TerrainType.Field, 0);
            TileRegion fRight = new TileRegion(2, TerrainType.Field, 0);

            road.AddAdjacent(fLeft); road.AddAdjacent(fRight);

            TileEdge nEdge = new TileEdge(fLeft, road, fRight);
            TileEdge eEdge = new TileEdge(fRight, fRight, fRight);
            TileEdge sEdge = new TileEdge(fRight, road, fLeft);
            TileEdge wEdge = new TileEdge(fLeft, fLeft, fLeft);

            return new TileData(id, new List<TileRegion> { road, fLeft, fRight }, new[] { nEdge, eEdge, sEdge, wEdge });
        }

        // 23. Поворот дороги (Юг-Запад)
        private TileData CreateTile23_RoadTurn(string id)
        {
            TileRegion road = new TileRegion(0, TerrainType.Road, 2);
            TileRegion fSmall = new TileRegion(1, TerrainType.Field, 0);
            TileRegion fBig = new TileRegion(2, TerrainType.Field, 0);

            road.AddAdjacent(fSmall); road.AddAdjacent(fBig);

            TileEdge fEdge = new TileEdge(fBig, fBig, fBig);
            TileEdge sEdge = new TileEdge(fBig, road, fSmall);
            TileEdge wEdge = new TileEdge(fSmall, road, fBig);

            return new TileData(id, new List<TileRegion> { road, fSmall, fBig }, new[] { fEdge, fEdge, sEdge, wEdge });
        }

        // 24. Перекресток Т-образный (Запад, Юг, Восток)
        private TileData CreateTile24_Cross3(string id)
        {
            TileRegion roadE = new TileRegion(0, TerrainType.Road, 1);
            TileRegion roadS = new TileRegion(1, TerrainType.Road, 1);
            TileRegion roadW = new TileRegion(2, TerrainType.Road, 1);
            TileRegion center = new TileRegion(3, TerrainType.Road, 0, false, false, false);
            TileRegion fNorth = new TileRegion(4, TerrainType.Field, 0); // Единое поле сверху
            TileRegion fSE = new TileRegion(5, TerrainType.Field, 0);
            TileRegion fSW = new TileRegion(6, TerrainType.Field, 0);

            center.AddAdjacent(roadE); center.AddAdjacent(roadS); center.AddAdjacent(roadW);
            roadE.AddAdjacent(fNorth); roadE.AddAdjacent(fSE);
            roadS.AddAdjacent(fSE); roadS.AddAdjacent(fSW);
            roadW.AddAdjacent(fSW); roadW.AddAdjacent(fNorth);

            TileEdge nEdge = new TileEdge(fNorth, fNorth, fNorth);
            TileEdge eEdge = new TileEdge(fNorth, roadE, fSE);
            TileEdge sEdge = new TileEdge(fSE, roadS, fSW);
            TileEdge wEdge = new TileEdge(fSW, roadW, fNorth);

            return new TileData(id, new List<TileRegion> { roadE, roadS, roadW, center, fNorth, fSE, fSW }, new[] { nEdge, eEdge, sEdge, wEdge }, center);
        }

        // 25. Перекресток 4 дороги
        private TileData CreateTile25_Cross4(string id)
        {
            TileRegion rN = new TileRegion(0, TerrainType.Road, 1);
            TileRegion rE = new TileRegion(1, TerrainType.Road, 1);
            TileRegion rS = new TileRegion(2, TerrainType.Road, 1);
            TileRegion rW = new TileRegion(3, TerrainType.Road, 1);
            TileRegion center = new TileRegion(4, TerrainType.Road, 0, false, false, false);
            TileRegion fNE = new TileRegion(5, TerrainType.Field, 0);
            TileRegion fSE = new TileRegion(6, TerrainType.Field, 0);
            TileRegion fSW = new TileRegion(7, TerrainType.Field, 0);
            TileRegion fNW = new TileRegion(8, TerrainType.Field, 0);

            center.AddAdjacent(rN); center.AddAdjacent(rE); center.AddAdjacent(rS); center.AddAdjacent(rW);
            rN.AddAdjacent(fNW); rN.AddAdjacent(fNE);
            rE.AddAdjacent(fNE); rE.AddAdjacent(fSE);
            rS.AddAdjacent(fSE); rS.AddAdjacent(fSW);
            rW.AddAdjacent(fSW); rW.AddAdjacent(fNW);

            TileEdge nEdge = new TileEdge(fNW, rN, fNE);
            TileEdge eEdge = new TileEdge(fNE, rE, fSE);
            TileEdge sEdge = new TileEdge(fSE, rS, fSW);
            TileEdge wEdge = new TileEdge(fSW, rW, fNW);

            return new TileData(id, new List<TileRegion> { rN, rE, rS, rW, center, fNE, fSE, fSW, fNW }, new[] { nEdge, eEdge, sEdge, wEdge }, center);
        }

        // ==========================================
        // ПАРСЕР ДЛЯ СЕТИ
        // ==========================================
        public TileData GetTileById(string id)
        {
            if (id.StartsWith("StartTile")) return CreateClassicStartTile(id);
            if (id.StartsWith("Tile1_")) return CreateTile1_Monastery(id);
            if (id.StartsWith("Tile2_")) return CreateTile2_MonasteryRoad(id);
            if (id.StartsWith("Tile3_")) return CreateTile3_FullCity(id, true);
            if (id.StartsWith("Tile5_")) return CreateTile5_City3_Field(id, true);
            if (id.StartsWith("Tile6_")) return CreateTile6_City3_Field(id, false);
            if (id.StartsWith("Tile7_")) return CreateTile7_City3_Road(id, true);
            if (id.StartsWith("Tile8_")) return CreateTile8_City3_Road(id, false);
            if (id.StartsWith("Tile9_")) return CreateTile9_CityCorner_Field(id, true);
            if (id.StartsWith("Tile10_")) return CreateTile10_CityCorner_Field(id, false);
            if (id.StartsWith("Tile11_")) return CreateTile11_CityCorner_Road(id, true);
            if (id.StartsWith("Tile12_")) return CreateTile12_CityCorner_Road(id, false);
            if (id.StartsWith("Tile13_")) return CreateTile13_CityTube(id, true);
            if (id.StartsWith("Tile14_")) return CreateTile14_CityTube(id, false);
            if (id.StartsWith("Tile15_")) return CreateTile15_TwoCitiesAdj(id);
            if (id.StartsWith("Tile16_")) return CreateTile16_TwoCitiesOpp(id);
            if (id.StartsWith("Tile17_")) return CreateTile17_CityCap(id);
            if (id.StartsWith("Tile18_")) return CreateTile18_CityCap_CurveWS(id);
            if (id.StartsWith("Tile19_")) return CreateTile19_CityCap_CurveSE(id);
            if (id.StartsWith("Tile20_")) return CreateTile20_CityCap_Cross3(id);
            if (id.StartsWith("Tile21_")) return CreateTile21_CityCap_Straight(id);
            if (id.StartsWith("Tile22_")) return CreateTile22_StraightRoad(id);
            if (id.StartsWith("Tile23_")) return CreateTile23_RoadTurn(id);
            if (id.StartsWith("Tile24_")) return CreateTile24_Cross3(id);
            if (id.StartsWith("Tile25_")) return CreateTile25_Cross4(id);

            return CreateClassicStartTile(id); // Резервный тайл
        }
    }
}