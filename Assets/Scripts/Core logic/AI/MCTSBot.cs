using System.Collections.Generic;
using UnityEngine;
using System;

namespace Assets.Scripts.Core_logic.AI
{
    public class MCTSBot
    {
        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Medium;

        // Личная телеметрия этого бота
        public AITelemetry Telemetry = new AITelemetry();

        // Общий бюджет симуляций на 1 ход
        private int _totalSimulationBudget = 1500;
        private System.Random _rng = new System.Random();

        /// <summary>
        /// Находит лучший ход. Выполняется в фоновом потоке процессора, чтобы не блокировать графику игры.
        /// </summary>
        /// <param name="realBoard"></param>
        /// <param name="currentTile"></param>
        /// <param name="remainingDeck"></param>
        /// <param name="bot"></param>
        /// <param name="opponent"></param>
        /// <returns></returns>
        public Move FindBestMove(Board realBoard, TileData currentTile, List<TileData> remainingDeck, Player bot, Player opponent)
        {
            List<Move> baseMoves = realBoard.GetAllValidMoves(currentTile);
            if (baseMoves.Count == 0) return null;

            // 1. РАСШИРЯЕМ ХОДЫ
            List<Move> expandedMoves = GenerateExpandedMoves(realBoard, currentTile, baseMoves, bot);
            if (expandedMoves.Count == 0) return baseMoves[0];

            // 2. ИНИЦИАЛИЗАЦИЯ УЗЛОВ (NODES) И ЭВРИСТИКА
            List<MCTSNode> nodes = new List<MCTSNode>();
            Telemetry.Clear();

            foreach (var m in expandedMoves)
            {
                MCTSNode newNode = new MCTSNode { NodeMove = m, Wins = 0, Simulations = 0 };

                // --- ЭВРИСТИКА (HEURISTIC PRIORS) ---
                // Если сложность Средняя или Высокая, мы даем умным ходам фору ("ложные" победы),
                // чтобы алгоритм UCB1 сразу обратил на них внимание.
                if (Difficulty >= BotDifficulty.Medium)
                {
                    ApplyHeuristicPriors(newNode, m, realBoard, currentTile, remainingDeck);
                }

                nodes.Add(newNode);
                Telemetry.CurrentNodes.Add(new MoveNodeStat { Position = m.Position, TotalSimulations = 0, Wins = 0 });
            }

            Telemetry.IsThinking = true;
            int totalSimsDone = 0;

            // 3. ГЛАВНЫЙ ЦИКЛ UCB1
            while (totalSimsDone < _totalSimulationBudget)
            {
                // Находим узел с наивысшим UCB1 Score
                int bestNodeIndex = 0;
                float bestUCB1 = -1f;

                for (int i = 0; i < nodes.Count; i++)
                {
                    float ucb1 = nodes[i].GetUCB1Score(totalSimsDone);
                    if (ucb1 > bestUCB1)
                    {
                        bestUCB1 = ucb1;
                        bestNodeIndex = i;
                    }
                }

                MCTSNode selectedNode = nodes[bestNodeIndex];
                Telemetry.CurrentNodes[bestNodeIndex].IsCurrentlySimulating = true; // Подсвечиваем активный нейрон

                // 4. СИМУЛЯЦИЯ (Playout) ДЛЯ ВЫБРАННОГО УЗЛА
                bool isWin = RunSinglePlayout(realBoard, currentTile, remainingDeck, bot, opponent, selectedNode.NodeMove);

                // 5. ОБНОВЛЕНИЕ ДАННЫХ
                selectedNode.Simulations++;
                if (isWin) selectedNode.Wins++;
                totalSimsDone++;

                Telemetry.CurrentNodes[bestNodeIndex].IsCurrentlySimulating = false;
                Telemetry.CurrentNodes[bestNodeIndex].TotalSimulations = selectedNode.Simulations;
                Telemetry.CurrentNodes[bestNodeIndex].Wins = (int)selectedNode.Wins;

                // Обновляем глобальную телеметрию для UI
                if (totalSimsDone % 50 == 0) // Не каждый кадр, ради оптимизации
                {
                    UpdateGlobalTelemetry(nodes);
                }
            }

            Telemetry.IsThinking = false;

            // Возвращаем ход, который алгоритм симулировал чаще всего (это математически самый надежный ход в MCTS)
            Move finalBestMove = nodes[0].NodeMove;
            int maxSims = -1;
            foreach (var n in nodes)
            {
                if (n.Simulations > maxSims) 
                { 
                    maxSims = n.Simulations; 
                    finalBestMove = n.NodeMove; 
                }
            }

            return finalBestMove;
        }

        // ------------------------------------------
        // БЛОК СИМУЛЯЦИИ ОДНОЙ ИГРЫ
        // ------------------------------------------
        private bool RunSinglePlayout(Board realBoard, TileData currentTile, List<TileData> remainingDeck, Player bot, Player opponent, Move firstMove)
        {
            Board simBoard = realBoard.Clone();
            Player simBot = bot.Clone();
            Player simOpponent = opponent.Clone();
            List<TileData> simDeck = new List<TileData>(remainingDeck);

            // Делаем первый (проверяемый) ход
            TileData simTile = currentTile.Clone();
            simTile.SetRotation(firstMove.Rotation);
            simBoard.PlaceTile(simTile, firstMove.Position);

            // --- ТОЧНЫЙ ПОДСЧЕТ АББАТА ---
            if (firstMove.RetrieveAbbot)
            {
                Vector2Int? foundPos = null;
                TileRegion foundRegion = null;

                // Ищем монастырь с нашим аббатом
                foreach (var kvp in simBoard.ActiveMonasteries)
                {
                    GlobalFeature feature = simBoard.GraphManager.GetFeature(kvp.Value);
                    if (feature.Meeples.ContainsKey(simBot.Id))
                    {
                        foundPos = kvp.Key;
                        foundRegion = kvp.Value;
                        break;
                    }
                }

                if (foundPos.HasValue)
                {
                    // Точный подсчет очков за квадрат 3x3 в симуляции
                    int points = 1;
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0) continue;
                            if (simBoard.GetTileAt(foundPos.Value + new Vector2Int(x, y)) != null) points++;
                        }
                    }

                    simBot.Score += points;
                    simBot.HasAbbot = true; // Аббат вернулся в руку
                    simBoard.GraphManager.GetFeature(foundRegion).Meeples.Clear();
                }
            }
            else if (firstMove.MeepleRegionId != -1)
            {
                TileRegion targetRegion = simTile.Regions.Find(r => r.Id == firstMove.MeepleRegionId);
                if (targetRegion != null && simBoard.CanPlaceMeeple(simBot, targetRegion))
                {
                    simBoard.PlaceMeeple(simBot, targetRegion);
                    if (firstMove.MeepleType == 1) simBot.HasAbbot = false;
                    else simBot.MeeplesAvailable--;
                }
            }

            simBoard.CheckAndScoreCompletedFeatures(simTile, new List<Player> { simBot, simOpponent });

            bool isBotTurn = false;

            // --- ДОИГРОВКА ПАРТИИ ---
            while (simDeck.Count > 0)
            {
                int randIndex = _rng.Next(0, simDeck.Count);
                TileData randomTile = simDeck[randIndex];
                simDeck.RemoveAt(randIndex);

                List<Move> randomMoves = simBoard.GetAllValidMoves(randomTile);
                if (randomMoves.Count > 0)
                {
                    Player currentPlayer = isBotTurn ? simBot : simOpponent;
                    Move selectedMove = null;

                    // Злой противник на Hard
                    if (Difficulty == BotDifficulty.Hard && _rng.NextDouble() > 0.5)
                    {
                        foreach (var m in randomMoves)
                        {
                            randomTile.SetRotation(m.Rotation);
                            // Простая эвристика симуляции: если ход приносит очки - делаем его
                            if (simBoard.CanPlaceTile(randomTile, m.Position))
                            {
                                selectedMove = m;
                                break;
                            }
                        }
                    }

                    if (selectedMove == null) selectedMove = randomMoves[_rng.Next(0, randomMoves.Count)];

                    randomTile.SetRotation(selectedMove.Rotation);
                    simBoard.PlaceTile(randomTile, selectedMove.Position);

                    // Установка миплов
                    if (currentPlayer.MeeplesAvailable > 0 && _rng.Next(0, 2) == 0)
                    {
                        foreach (var r in randomTile.Regions)
                        {
                            if (r.IsPlaceable && simBoard.CanPlaceMeeple(currentPlayer, r))
                            {
                                simBoard.PlaceMeeple(currentPlayer, r);
                                break;
                            }
                        }
                    }

                    simBoard.CheckAndScoreCompletedFeatures(randomTile, new List<Player> { simBot, simOpponent });
                }
                isBotTurn = !isBotTurn;
            }

            simBoard.ScoreEndGameFields(new List<Player> { simBot, simOpponent });
            return simBot.Score > simOpponent.Score;
        }

        // ------------------------------------------
        // БЛОК ЭВРИСТИКИ (ИНТУИЦИЯ БОТА)
        // ------------------------------------------
        private void ApplyHeuristicPriors(MCTSNode node, Move m, Board board, TileData tile, List<TileData> deck)
        {
            int bonusWins = 0;
            int bonusSims = 0;

            // 1. Возврат аббата: Если аббата забирают - это перспективно
            if (m.RetrieveAbbot) 
            { 
                bonusWins += 15; 
                bonusSims += 15; 
            }

            // 2. Оценка регионов
            if (m.MeepleRegionId != -1)
            {
                TileRegion region = tile.Regions.Find(r => r.Id == m.MeepleRegionId);
                if (region != null)
                {
                    // Базовое поощрение (Бот любит ставить миплов)
                    bonusWins += 5; 
                    bonusSims += 5;

                    // Фермеры в начале игры - ПЛОХО
                    if (region.Type == TerrainType.Field)
                    {
                        if (deck.Count > 30) 
                        { 
                            bonusWins -= 5; 
                            bonusSims += 20; 
                        }
                        else 
                        { 
                            bonusWins += 15; 
                            bonusSims += 15; 
                        }
                    }
                    // Город - ХОРОШО
                    if (region.Type == TerrainType.City)
                    {
                        // Форсируем попытки занять город
                        bonusWins += 15;  
                        bonusSims += 15;
                    }

                    // Монастырь (Аббат или Мипл) - ОЧЕНЬ ХОРОШО
                    if (region.Type == TerrainType.Monastery)
                    {
                        // Бот обожает ставить Аббата. Даем ему гигантскую фору в UCB1
                        bonusWins += 25; 
                        bonusSims += 25;
                    }
                }
            }

            // --- АГРЕССИВНЫЙ ЗАХВАТ ЧУЖИХ ГОРОДОВ (Только на Hard) ---
            if (Difficulty == BotDifficulty.Hard && m.MeepleRegionId != -1) // Захват делают без мипла на текущем тайле
            {
                // Проверяем 4 клетки вокруг хода бота
                Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

                foreach (var offset in offsets)
                {
                    TileData neighborTile = board.GetTileAt(m.Position + offset);
                    if (neighborTile != null)
                    {
                        // Ищем регионы соседа
                        foreach (var neighborRegion in neighborTile.Regions)
                        {
                            // Если у соседа есть Город или Дорога
                            if (neighborRegion.Type == TerrainType.City || neighborRegion.Type == TerrainType.Road)
                            {
                                GlobalFeature neighborFeature = board.GraphManager.GetFeature(neighborRegion);

                                // Если на этом объекте ЕСТЬ мипл, и он ЧУЖОЙ
                                // (В Каркассоне мы можем пристроиться рядом, чтобы следующим ходом соединить города и украсть очки)
                                if (neighborFeature.Meeples.Count > 0 && !neighborFeature.Meeples.ContainsKey(999)) // 999 = ID бота
                                {
                                    // Огромный бонус. Это агрессивное вторжение.
                                    bonusWins += 45;
                                    bonusSims += 45;
                                    break; // Хватит одного чужого объекта для бонуса
                                }
                            }
                        }
                    }
                }
            }

            node.Wins += bonusWins;
            node.Simulations += bonusSims;
        }

        // Вспомогательные методы GenerateExpandedMoves и UpdateGlobalTelemetry
        // делают то же самое, что и в прошлом коде: собирают список ходов и обновляют Telemetry).

        private void UpdateGlobalTelemetry(List<MCTSNode> nodes)
        {
            float bestWinRate = -1f;
            Telemetry.TotalSimulationsCompleted = 0;

            foreach (var n in nodes)
            {
                Telemetry.TotalSimulationsCompleted += n.Simulations;
                if (n.Simulations > 0)
                {
                    float wr = n.Wins / n.Simulations;
                    if (wr > bestWinRate)
                    {
                        bestWinRate = wr;
                        Telemetry.BestMoveWinRate = wr;
                        Telemetry.BestMoveCoord = $"({n.NodeMove.Position.x}, {n.NodeMove.Position.y})";
                    }
                }
            }
        }

        private List<Move> GenerateExpandedMoves(Board board, TileData tile, List<Move> baseMoves, Player bot)
        {
            List<Move> expanded = new List<Move>();

            foreach (Move move in baseMoves)
            {
                // ВАРИАНТ 0: Поставить тайл и ничего не делать
                expanded.Add(new Move(move.Position, move.Rotation, -1, 0, false));

                // Создаем виртуальную доску для проверки миплов/аббатов
                Board tempBoard = board.Clone();
                TileData tempTile = tile.Clone();
                tempTile.SetRotation(move.Rotation);
                tempBoard.PlaceTile(tempTile, move.Position);

                // ВАРИАНТ А: УСТАНОВКА (Если в руке есть Мипл или Аббат)
                if (bot.MeeplesAvailable > 0 || bot.HasAbbot)
                {
                    foreach (var region in tempTile.Regions)
                    {
                        if (region.IsPlaceable && tempBoard.CanPlaceMeeple(bot, region))
                        {
                            // Обычный мипл
                            if (bot.MeeplesAvailable > 0 && region.Type != TerrainType.Monastery)
                            {
                                expanded.Add(new Move(move.Position, move.Rotation, region.Id, 0, false));
                            }

                            // Аббат
                            if (bot.HasAbbot && region.Type == TerrainType.Monastery)
                            {
                                expanded.Add(new Move(move.Position, move.Rotation, region.Id, 1, false));
                            }
                        }
                    }
                }

                // ВАРИАНТ Б: СНЯТИЕ АББАТА (Если он уже на поле)
                if (!bot.HasAbbot)
                {
                    // Ищем все монастыри на столе
                    foreach (var kvp in tempBoard.ActiveMonasteries)
                    {
                        GlobalFeature feature = tempBoard.GraphManager.GetFeature(kvp.Value);

                        // Если в этом монастыре сидит Аббат нашего бота
                        if (feature.Meeples.ContainsKey(bot.Id))
                        {
                            // Добавляем новый вариант хода: Поставить тайл и СНЯТЬ Аббата!
                            expanded.Add(new Move(move.Position, move.Rotation, -1, 0, true));

                            // Выходим из цикла, так как снять можно только одного аббата за ход
                            break;
                        }
                    }
                }
            }

            return expanded;
        }
    }
}