using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FREQSeq
{
	/// <summary>
	/// Modified from SmithWatermanGotoh class in Bio C# library
	/// 
	/// This class is a simpler version, which doesn't do the traceback step, only reports the score
	/// and is hopefully immutable for parrallel  purposes
   
	/// </summary>
	public sealed class ScoreOnlySmithWatermanGotoh
	{
		private enum LastMove : byte
		{
			Diagonal = 1,
			Insertion = 2,
			Deletion = 3,
			None = 4}
		;

		private struct MatrixPosition
		{
			public float score;
			public LastMove lastMove;

			public MatrixPosition (float score, LastMove lastMove)
			{
				this.score = score;
				this.lastMove = lastMove;
			}
		}

		public static float GetSmithWatermanScore (string databaseSequence, string querySequence, SimpleSubstitutionMatrix substitutionMatrix)
		{
			int sizeCutoff = databaseSequence.Length + 10;
			if (querySequence.Length > sizeCutoff) {
				querySequence = querySequence.Substring (0, sizeCutoff);
			}
			//Get relevant variables
			float penaltyGapExist = -substitutionMatrix.gapExistPenalty;
			float penaltyGapExtend = -substitutionMatrix.gapExtendPenalty;
			float MatchScore = substitutionMatrix.MatchScore;
			float MisMatchScore = substitutionMatrix.MisMatchScore;
			float[] Dk_1 = new float[databaseSequence.Length + 1];
			float[] Qk_1 = new float[querySequence.Length + 1];
			float highScore = -999;
			//Initialize matrix
			MatrixPosition[,] matrix = new MatrixPosition[databaseSequence.Length + 1, querySequence.Length + 1];
			//First set all values in the horizontal (database) to zero
			for (int i = 0; i < matrix.GetLength (0); i++) {
				matrix [i, 0] = new MatrixPosition (0, LastMove.None);
				Dk_1 [i] = 0 - penaltyGapExist;
			}

			//Second set all values in the vertical (query) to zero
			for (int i = 0; i < matrix.GetLength (1); i++) {
				matrix [0, i] = new MatrixPosition (0, LastMove.None);
				Qk_1 [i] = 0 - penaltyGapExist;
			}

			//Go down use dimension of the query
			for (int k = 1; k < matrix.GetLength (1); k++) {
				for (int i = 1; i < matrix.GetLength (0); i++) {
					//i=database sequence in the horizontal
					//k=query sequence in the vertical
					//the database sequence is in the horizontal, the query in the vertical axis of the matrix
					//Diagonal score is the previous score and in addition the similarityValue;
					float ScoreUpdate = databaseSequence [i - 1] == querySequence [k - 1] ? MatchScore : MisMatchScore;
					float scoreDiagonal = matrix [i - 1, k - 1].score + ScoreUpdate;

					//Find the highest scoring insertion, testing all matrix to the upper side;
					float downScoreInsertion;
					downScoreInsertion = Math.Max (matrix [i, k - 1].score - penaltyGapExist, Dk_1 [i] - penaltyGapExtend);
					Dk_1 [i] = downScoreInsertion;

					//Find the highest scroing deletion, testing all matrix entries to the left side
					float rightScoreDeletion;
					rightScoreDeletion = Math.Max (matrix [i - 1, k].score - penaltyGapExist, Qk_1 [k] - penaltyGapExtend);
					Qk_1 [k] = rightScoreDeletion;
					var current = GetMaximumPosition (scoreDiagonal, downScoreInsertion, rightScoreDeletion, 0);
					matrix [i, k] = current; 

					//Updating the highest scoring matrix entry
					if (current.score > highScore) {
						//new highscore
						highScore = current.score;
					}
				}
			}
			System.Diagnostics.Debug.Assert (highScore <= MatchScore * databaseSequence.Length && highScore <= MatchScore * querySequence.Length);
			return highScore;
		}

		private static MatrixPosition GetMaximumPosition (float scoreDiagonal, float scoreInsertion, float scoreDeletion, float scoreNone)
		{
			MatrixPosition position;

			if (scoreDiagonal > scoreNone) {
				//exclude scoreNone
				if (scoreDiagonal >= scoreInsertion) {
					//exclude scoreNone & scoreInsertion
					if (scoreDiagonal >= scoreDeletion) {
						//exclude scoreNone & scoreInsertion & scoreDeletion => DIAGONAL
						position = new MatrixPosition (scoreDiagonal, LastMove.Diagonal);
					} else {
						//exclude scoreNone & scoreInsertion & scoreDiagonal => DELETION
						position = new MatrixPosition (scoreDeletion, LastMove.Deletion);
					}
				} else {
					//exclude scoreNone & scoreDiagonal
					if (scoreInsertion > scoreDeletion) {
						//exclude scoreNone & scoreDiagonal & scoreDeletion => INSERTION
						position = new MatrixPosition (scoreInsertion, LastMove.Insertion);
					} else {
						//exclude scoreNone &scoreDiagonal & scoreInsertion => DELETION
						position = new MatrixPosition (scoreDeletion, LastMove.Deletion);

					}
				}
			} else {
				//exclude scoreDiagonal
				if (scoreInsertion > scoreNone) {
					//exclude scoreDiagonal & scoreNone
					if (scoreInsertion > scoreDeletion) {
						//exclude scoreDiagonal & scoreNone & scoreDeletion => INSERTION
						position = new MatrixPosition (scoreInsertion, LastMove.Insertion);
					} else {
						//exclude scoreDiagonal & scoreNone & scoreInsertion => DELETION
						position = new MatrixPosition (scoreDeletion, LastMove.Deletion);
					}
				} else {
					//exclude scoreDiagonal & scoreInsertion
					if (scoreDeletion > scoreNone) {
						//exclude scoreDiagonal & scoreInsertion & scoreNone => DELETION
						position = new MatrixPosition (scoreDeletion, LastMove.Deletion);

					} else {
						//exclude scoreDiagonal & scoreInsertion & scoreDeletion =>NONE
						position = new MatrixPosition (scoreNone, LastMove.None);
					}
				}
			}
			return position; //That was annoying
		}

		private static MatrixPosition GetMaximumPosition (float scoreDiagonal, float scoreInsertion, float scoreDeletion)
		{
			MatrixPosition position;


			//exclude scoreNone

			if (scoreDiagonal >= scoreInsertion) {
				//exclude scoreNone & scoreInsertion

				if (scoreDiagonal >= scoreDeletion) {
					//exclude scoreNone & scoreInsertion & scoreDeletion => DIAGONAL

					position = new MatrixPosition (scoreDiagonal, LastMove.Diagonal);
				} else {
					//exclude scoreNone & scoreInsertion & scoreDiagonal => DELETION
					position = new MatrixPosition (scoreDeletion, LastMove.Deletion);

				}
			} else {
				//exclude scoreNone & scoreDiagonal


				if (scoreInsertion > scoreDeletion) {
					//exclude scoreNone & scoreDiagonal & scoreDeletion => INSERTION
					position = new MatrixPosition (scoreInsertion, LastMove.Insertion);
				} else {
					//exclude scoreNone &scoreDiagonal & scoreInsertion => DELETION
					position = new MatrixPosition (scoreDeletion, LastMove.Deletion);

				}
			}



			return position; //That was annoying
		}
	}

	/// <summary>
	/// Substitution matrix, hopefully immutable
	/// </summary>
	public sealed class SimpleSubstitutionMatrix
	{
		public readonly float gapExistPenalty;
		public readonly float gapExtendPenalty;
		public readonly float MatchScore;
		public readonly float MisMatchScore;

		public SimpleSubstitutionMatrix (float match, float mismatch, float gapexist, float gapExtend)
		{
			gapExistPenalty = gapexist;
			gapExtendPenalty = gapExtend;
			MatchScore = match;
			MisMatchScore = mismatch;
		}

		public SimpleSubstitutionMatrix Clone ()
		{
			return new SimpleSubstitutionMatrix (MatchScore, MisMatchScore, gapExistPenalty, gapExtendPenalty);
		}
	}
}

