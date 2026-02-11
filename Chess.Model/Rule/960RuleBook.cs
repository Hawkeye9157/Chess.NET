/*
 * This code was created by Clint Sipes following logic and practices from earlier authors
 */

namespace Chess.Model.Rule
{
    using Chess.Model.Command;
    using Chess.Model.Data;
    using Chess.Model.Game;
    using Chess.Model.Piece;
    using Chess.Model.Visitor;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    public class _960RuleBook : IRulebook
    {
        /// <summary>
        /// Represents the check rule of a standard chess game.
        /// </summary>
        private readonly CheckRule checkRule;

        /// <summary>
        /// Represents the end rule of a standard chess game.
        /// </summary>
        private readonly EndRule endRule;

        /// <summary>
        /// Represents the movement rule of a standard chess game.
        /// </summary>
        private readonly MovementRule movementRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="_960RuleBook"/> class.
        /// </summary>
        public _960RuleBook()
        {
            var threatAnalyzer = new ThreatAnalyzer();
            var castlingRule = new CastlingRule(threatAnalyzer);
            var enPassantRule = new EnPassantRule();
            var promotionRule = new PromotionRule();

            this.checkRule = new CheckRule(threatAnalyzer);
            this.movementRule = new MovementRule(castlingRule, enPassantRule, promotionRule, threatAnalyzer);
            this.endRule = new EndRule(this.checkRule, this.movementRule);

        }

        /// <summary>
        /// Creates a new chess game according to the standard rulebook.
        /// </summary>
        /// <returns>The newly created chess game.</returns>
        public ChessGame CreateGame()
        {
            IEnumerable<PlacedPiece> makeBaseLine(int row, Color color)
            {
                List<int> cols = new List<int>();
                Random random = new Random();
                for(int i = 0; i < 8; i++)
                {
                    cols.Add(i);
                }

                int curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Rook(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Knight(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Bishop(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Queen(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new King(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Bishop(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Knight(color));
                cols.Remove(curNum);

                curNum = cols[random.Next(0, cols.Count())];
                yield return new PlacedPiece(new Position(row, curNum), new Rook(color));
            }

            IEnumerable<PlacedPiece> makePawns(int row, Color color) =>
                Enumerable.Range(0, 8).Select(
                    i => new PlacedPiece(new Position(row, i), new Pawn(color))
                );

            IImmutableDictionary<Position, ChessPiece> makePieces(int pawnRow, int baseRow, Color color)
            {
                var pawns = makePawns(pawnRow, color);
                var baseLine = makeBaseLine(baseRow, color);
                var pieces = baseLine.Union(pawns);
                var empty = ImmutableSortedDictionary.Create<Position, ChessPiece>(PositionComparer.DefaultComparer);
                return pieces.Aggregate(empty, (s, p) => s.Add(p.Position, p.Piece));
            }

            var whitePlayer = new Player(Color.White);
            var whitePieces = makePieces(1, 0, Color.White);
            var blackPlayer = new Player(Color.Black);
            var blackPieces = makePieces(6, 7, Color.Black);
            var board = new Board(whitePieces.AddRange(blackPieces));

            return new ChessGame(board, whitePlayer, blackPlayer);
        }

        /// <summary>
        /// Gets the status of a chess game, according to the standard rulebook.
        /// </summary>
        /// <param name="game">The game state to be analyzed.</param>
        /// <returns>The current status of the game.</returns>
        public Status GetStatus(ChessGame game)
        {
            return this.endRule.GetStatus(game);
        }

        /// <summary>
        /// Gets all possible updates (i.e., future game states) for a chess piece on a specified position,
        /// according to the standard rulebook.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="position">The position to be analyzed.</param>
        /// <returns>A sequence of all possible updates for a chess piece on the specified position.</returns>
        public IEnumerable<Update> GetUpdates(ChessGame game, Position position)
        {
            var piece = game.Board.GetPiece(position, game.ActivePlayer.Color);
            var updates = piece.Map(
                p =>
                {
                    var moves = this.movementRule.GetCommands(game, p);
                    var turnEnds = moves.Select(c => new SequenceCommand(c, EndTurnCommand.Instance));
                    var records = turnEnds.Select
                    (
                        c => new SequenceCommand(c, new SetLastUpdateCommand(new Update(game, c)))
                    );
                    var futures = records.Select(c => c.Execute(game).Map(g => new Update(g, c)));
                    return futures.FilterMaybes().Where
                    (
                        e => !this.checkRule.Check(e.Game, e.Game.PassivePlayer)
                    );
                }
            );

            return updates.GetOrElse(Enumerable.Empty<Update>());
        }
    }
}
