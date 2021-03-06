﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace inCapsulam.Optimization.Methods
{
    public class GeneticApproachMethod
    {
        public static Random rndm = new Random();

        [Serializable()]
        /// <summary>
        /// Container with GA settings.
        /// </summary>
        public class Settings
        {
            public const short MutationReal = 0;
            public const short MutationBoolean = 1;

            public const short CrossoverTwoPoints = 0;
            public const short CrossoverUniform = 1;
            public const short CrossoverReal = 2;

            public const short SelectionTournament = 0;
            public const short SelectionRange = 1;
            public const short SelectionProportional = 2;

            public const short PostSelectionBest = 0;
            public const short PostSelectionNew = 1;
            public const short PostSelectionHalfOldHalfNew = 2;

            public const short PostOptimizationOneStepCorrection = 0;
            public const short PostOptimizationGradualCorrection = 1;

            public const short PenaltyEmpty = 0;
            public const short PenaltyDynamic = 1;
            public const short PenaltyAdaptive = 2;
            public const short PenaltyConstraint = 3;

            public short MutationMode = 1;
            public short CrossoverMode = 0;
            public short ParentSelectionMode = 0;
            public short ChildSelectionMode = 0;
            public short PostSelectionMode = 0;
            public short PostOptimizationMode = 0;
            public short PenaltyMode = 2;

            /// <summary>
            /// The left border of first population interval.
            /// </summary>
            public double LeftBorderOfFirstPopulationInterval = -10;
            /// <summary>
            /// The right border of first population interval.
            /// </summary>
            public double RightBorderOfFirstPopulationInterval = 10;
            /// <summary>
            /// The population count.
            /// </summary>
            public int PopulationCount = 100;
            /// <summary>
            /// The iterations maximum number.
            /// </summary>
            public int IterationsMaxNumber = 50;
            /// <summary>
            /// The possibility of mutation for a random solution.
            /// </summary>
            public double MutationCoefficient = 0.01;
            /// <summary>
            /// The possibility of post-optimization process for each solution in population.
            /// </summary>
            public double PostOptimizationCoefficient = 0.00;
            /// <summary>
            /// The size of the tournament in tournament selection.
            /// </summary>
            public int TournamentSize = 50;
            /// <summary>
            /// The number of saved best solutions.
            /// </summary>
            public int ElitismNumber = 1;
            /// <summary>
            /// Represents the desired precision of solution.
            /// </summary>
            public double Precision = 0.001;
            /// <summary>
            /// The bits count in chromosome
            /// </summary>
            public short BitCount = 14;
            /// <summary>
            /// Predicted minimum value of objective function. Used in fitness estimation.
            /// </summary>
            public double ObjectiveFunctionMinValue = 0;
            /// <summary>
            /// Number of threads to use in fitness calculation
            /// </summary>
            public int ThreadsCount = 1;

            private double _Precision = 0;
            private short _DigitsAfterPoint;
            public short DigitsAfterPoint
            {
                get
                {
                    if (_Precision != Precision)
                    {
                        _Precision = Precision;
                        _DigitsAfterPoint = (short)Math.Ceiling(Math.Abs(Math.Log10(Precision)));
                    }
                    return _DigitsAfterPoint;
                }
            }

            public bool UseGreyCode;
            public bool UseElitism;
            public bool UseLocalTournament;
            public bool UsePositiveOnly;
            public bool UseChildSelection;

            public Settings() { }
        }

        [Serializable()]
        public class Process
        {
            [NonSerialized()]
            public Task task;

            public Settings current = new Settings();

            public int CurrentIteration = 0;

            [NonSerialized()]
            public SolutionGA[] Population;

            int threadsRunning;

            public List<double> Logging_BestValue = new List<double>();
            public List<double> Logging_WorstValue = new List<double>();
            public List<double> Logging_AverageValue = new List<double>();

            public List<double> Logging_BestFitness = new List<double>();
            public List<double> Logging_WorstFitness = new List<double>();
            public List<double> Logging_AverageFitness = new List<double>();

            public List<double[]> Logging_BestFeasible = new List<double[]>();
            public List<double[]> Logging_BestSolution = new List<double[]>();
            public List<double[]> Logging_WorstSolution = new List<double[]>();

            public List<int> Logging_ParentsCount = new List<int>();
            public List<int> Logging_FeasiblesCount = new List<int>();

            public List<double[]> Logging_PopulationValues = new List<double[]>();

            public int Logging_ObjectiveFunctionCalculations = 0;
            public long Logging_TimeElapsed = 0;

            [NonSerialized()]
            System.Diagnostics.Stopwatch swatch;

            public Process()
            {

            }

            public Process(Task theTask, Settings settings)
            {
                task = theTask;
                current = settings;
                Population = new SolutionGA[current.PopulationCount];
                for (int i = 0; i < Population.Length; i++)
                {
                    Population[i] = new SolutionGA(this, true);
                }
            }

            private double[][] ProximityMatrix;

            private void CalculateProximityMatrix()
            {
                ProximityMatrix = new double[current.PopulationCount][];
                for (int i = 0; i < Population.Length; i++)
                {
                    ProximityMatrix[i] = new double[Population.Length];
                    for (int j = 0; j < Population.Length; j++)
                    {
                        for (int k = 0; k < task.Target.Parameters.Length; k++)
                        {
                            ProximityMatrix[i][j] += Math.Pow(Population[i][k] - Population[j][k], 2);
                        }
                        ProximityMatrix[i][j] = Math.Sqrt(ProximityMatrix[i][j]);
                    }
                }
            }

            public double[] RunToTheEnd()
            {
                swatch = new System.Diagnostics.Stopwatch();
                swatch.Start();
                for (int i = 0; i < current.IterationsMaxNumber; i++)
                {
                    Run();
                    if (Logging_ParentsCount[CurrentIteration] < 2)
                    {
                        break;
                    }
                    else
                    {
                        CurrentIteration++;
                    }
                }

                if (CurrentIteration > 0)
                {
                    Log();
                    swatch.Stop();
                    return Logging_BestSolution[CurrentIteration - 1];
                }
                else
                {
                    swatch.Stop();
                    return null;
                }
            }

            public double ViolationsCount = 0;

            public double AverageFitness = 0;

            public double[] AverageViolations;
            public double AverageViolationsSquaredSum;

            void CalculateViolationsCount()
            {
                int ViolationsCount = 0;
                for (int i = 0; i < Population.Length; i++)
                {
                    if (!Population[i].IsFeasible) ViolationsCount++;
                }
            }

            void CalculateAverageFitness()
            {
                AverageFitness = 0;
                for (int i = 0; i < Population.Length; i++)
                {
                    AverageFitness += 1 / (1 + Population[i].Value - current.ObjectiveFunctionMinValue);
                }
                AverageFitness /= Population.Length;
            }

            void CalculateAverageViolations()
            {
                AverageViolations = new double[task.Constraints.Length];
                for (int i = 0; i < AverageViolations.Length; i++)
                {
                    for (int j = 0; j < Population.Length; j++)
                    {
                        AverageViolations[i] += Math.Abs(Population[j].Violation);
                    }
                    AverageViolations[i] /= Population.Length;
                    AverageViolationsSquaredSum += AverageViolations[i] * AverageViolations[i];
                }

            }

            void CalculateFitnessOf(object info)
            {
                SolutionGA[] solutions = (SolutionGA[])info;
                for (int i = 0; i < solutions.Length; i++)
                {
                    lock (solutions[i])
                    {
                        solutions[i].SetFitness();
                        Logging_ObjectiveFunctionCalculations++;
                    }
                }
                Interlocked.Decrement(ref threadsRunning);
            }

            void CalculateFitness(SolutionGA[] solutions)
            {
                List<SolutionGA>[] subPopulations = new List<SolutionGA>[current.ThreadsCount];

                for (int i = 0; i < solutions.Length; i++)
                {
                    int index = (i + 1) % current.ThreadsCount;
                    if (object.Equals(subPopulations[index], null))
                        subPopulations[index] = new List<SolutionGA>();
                    subPopulations[index].Add(solutions[i]);
                }

                for (int i = 0; i < subPopulations.Length; i++)
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback(CalculateFitnessOf), subPopulations[i].ToArray());
                    Interlocked.Increment(ref threadsRunning);
                }

                while (threadsRunning > 0) { }
            }

            public void Run()
            {
                CalculateFitness(Population);
                Log();

                if (current.UseLocalTournament) CalculateProximityMatrix();
                if (current.PenaltyMode == Settings.PenaltyAdaptive) CalculateViolationsCount();
                CalculateAverageFitness();

                int[] parentsIds = ParentSelection(Population);
                Logging_ParentsCount.Add(parentsIds.Length);

                if (parentsIds.Length < 2) return;
                SolutionGA[] childs = Crossover(parentsIds);

                for (int i = 0; i < childs.Length; i++)
                {
                    childs[i].Mutate();
                }

                CalculateFitness(childs);

                if (current.UseChildSelection)
                {
                    int[] childIds = ChildSelection(childs);
                    SolutionGA[] childsNew = new SolutionGA[childIds.Length];
                    for (int i = 0; i < childsNew.Length; i++)
                    {
                        childsNew[i] = childs[childIds[i]];
                    }
                    PostSelection(childsNew);
                }
                else
                {
                    PostSelection(childs);
                }

                PostOptimization();
            }

            void Log()
            {
                List<int> populationIndexes = new List<int>();
                List<double> fitnesses = new List<double>();
                List<double> cleanFitnesses = new List<double>();
                List<double> values = new List<double>();

                int feasiblesCount = 0;
                int[] indexes = new int[1];
                for (int i = 0; i < Population.Length; i++)
                {
                    cleanFitnesses.Add(Population[i].CleanFitness);
                    values.Add(Population[i].Value);
                    fitnesses.Add(Population[i].Fitness);

                    populationIndexes.Add(i);

                    if (Population[i].IsFeasible)
                    {
                        feasiblesCount++;
                    }
                }

                double[] cleanFitnessesArray = cleanFitnesses.ToArray();
                double[] valuesArray = values.ToArray();
                double[] fitnessesArray = fitnesses.ToArray();

                Logging_PopulationValues.Add(valuesArray);

                Various.OrderByDesc(ref cleanFitnessesArray, ref indexes, true);
                Logging_BestFitness.Add(cleanFitnesses[indexes.First()]);
                Logging_AverageFitness.Add(cleanFitnesses.Average());
                Logging_WorstFitness.Add(cleanFitnesses[indexes.Last()]);

                Various.OrderByDesc(ref fitnessesArray, ref indexes, true);
                Logging_BestSolution.Add(Population[populationIndexes[indexes.First()]].ParametersDouble);
                Logging_WorstSolution.Add(Population[populationIndexes[indexes.Last()]].ParametersDouble);
                Logging_FeasiblesCount.Add(feasiblesCount);

                Various.OrderByDesc(ref valuesArray, ref indexes, false);
                Logging_BestValue.Add(valuesArray[0]);
                Logging_WorstValue.Add(valuesArray.Last());
                Logging_AverageValue.Add(valuesArray.Average());

                //Logging_TimeElapsed = swatch.ElapsedMilliseconds;
            }

            /*
             * 
             * Selection operations block
             * 
             * */
            private int[] ParentSelection(SolutionGA[] parents)
            {
                switch (current.ParentSelectionMode)
                {
                    case Settings.SelectionProportional:
                        return SelectionProportional(parents).ToArray();
                    case Settings.SelectionRange:
                        return SelectionRange(parents).ToArray();
                    case Settings.SelectionTournament:
                        return SelectionTournament(parents).ToArray();
                }
                return SelectionTournament(parents).ToArray();
            }

            private int[] ChildSelection(SolutionGA[] childs)
            {
                switch (current.ParentSelectionMode)
                {
                    case Settings.SelectionProportional:
                        return SelectionProportional(childs).ToArray();
                    case Settings.SelectionRange:
                        return SelectionRange(childs).ToArray();
                    case Settings.SelectionTournament:
                        return SelectionTournament(childs).ToArray();
                }
                return SelectionTournament(childs).ToArray();
            }

            private List<int> SelectionProportional(SolutionGA[] solutions)
            {
                // Parents indexes
                List<int> parents = new List<int>();
                // Fitness values
                double[] fitnesses = new double[solutions.Length];
                // Maximum value of fitnesses
                double fitnessMax = double.MinValue;
                // Normed fitness values
                double[] fitnessesNormed = new double[solutions.Length];
                // Probability values in the form: p1, p1+p2, p1+p2+p3, ..., p1+...+pN
                double[] p_additive = new double[solutions.Length];

                for (int i = 0; i < solutions.Length; i++)
                {
                    fitnesses[i] = solutions[i].Fitness;
                    if (fitnesses[i] > fitnessMax)
                    {
                        fitnessMax = fitnesses[i];
                    }
                }
                for (int i = 0; i < solutions.Length; i++)
                {
                    fitnessesNormed[i] = (fitnesses[i] + 1) / fitnessMax;
                }
                double sumOfNormed = fitnessesNormed.Sum();
                for (int i = 0; i < solutions.Length; i++)
                {
                    p_additive[i] = fitnessesNormed[i] / sumOfNormed;
                    if (i > 0) p_additive[i] += p_additive[i - 1];
                }
                // Realisation of geometrical intepretation of what probability is
                for (int i = 0; i < solutions.Length; i++)
                {
                    double p_toChoose = rndm.NextDouble();
                    for (int j = 0; j < p_additive.Length; j++)
                    {
                        if (p_toChoose > p_additive[j] && p_toChoose < p_additive[j + 1])
                        {
                            if (!parents.Contains(j + 1)) parents.Add(j + 1);
                            break;
                        }
                        else if (p_toChoose < p_additive[0])
                        {
                            if (!parents.Contains(0)) parents.Add(0);
                            break;
                        }
                    }
                }
                return parents;
            }

            private List<int> SelectionRange(SolutionGA[] solutions)
            {
                // Parents indexes
                List<int> parents = new List<int>();
                // Fitness values
                double[] fitnesses = new double[solutions.Length];
                // Divider
                double divider = (solutions.Length + 1) * solutions.Length;
                // Probability values in the form: p1, p1+p2, p1+p2+p3, ..., p1+...+pN
                double[] p_additive = new double[solutions.Length];
                // Indexes of solutions
                int[] indexes = new int[1];

                for (int i = 0; i < solutions.Length; i++)
                {
                    fitnesses[i] = solutions[i].Fitness;
                }
                // Reordering array of fitness values
                Various.OrderByDesc(ref fitnesses, ref indexes, true);

                for (int i = 0; i < solutions.Length; i++)
                {
                    p_additive[i] = 2 * (double)(solutions.Length - i) / divider;
                    if (i > 0) p_additive[i] += p_additive[i - 1];
                }

                // Realisation of geometrical intepretation of what probability is
                for (int i = 0; i < solutions.Length; i++)
                {
                    double p_toChoose = rndm.NextDouble();
                    for (int j = 0; j < p_additive.Length; j++)
                    {
                        if (p_toChoose > p_additive[j] && p_toChoose < p_additive[j + 1])
                        {
                            if (!parents.Contains(indexes[j + 1])) parents.Add(indexes[j + 1]);
                            break;
                        }
                        else if (p_toChoose < p_additive[0])
                        {
                            if (!parents.Contains(indexes[0])) parents.Add(indexes[0]);
                            break;
                        }
                    }
                }
                return parents;
            }

            private List<int> SelectionTournament(SolutionGA[] solutions)
            {
                // Parents indexes
                List<int> parents = new List<int>();
                // Fitness values
                double[] fitnesses = new double[solutions.Length];
                for (int i = 0; i < solutions.Length; i++)
                {
                    fitnesses[i] = solutions[i].Fitness;
                }
                for (int i = 0; i < solutions.Length; i++)
                {
                    int[] tournamentSolutionGAsIndexes;
                    if (current.UseLocalTournament)
                    {
                        double[] orderedProximities = new double[solutions.Length];
                        for (int j = 0; j < solutions.Length; j++)
                        {
                            orderedProximities[j] = ProximityMatrix[i][j];
                        }
                        int[] indexesOfProximities = new int[1];
                        // Reordering
                        Various.OrderByDesc(ref orderedProximities, ref indexesOfProximities, false);
                        tournamentSolutionGAsIndexes = new int[current.TournamentSize];
                        for (int j = 0; j < tournamentSolutionGAsIndexes.Length; j++)
                        {
                            tournamentSolutionGAsIndexes[j] = indexesOfProximities[j];
                        }
                    }
                    else
                    {
                        tournamentSolutionGAsIndexes = new int[current.TournamentSize];
                        int k = i;
                        for (int j = 0; j < tournamentSolutionGAsIndexes.Length; j++)
                        {
                            tournamentSolutionGAsIndexes[j] = k;
                            k = k < solutions.Length - 1 ? k + 1 : 0;
                        }
                    }
                    double bestFitness = fitnesses[tournamentSolutionGAsIndexes[0]];
                    int bestFitnessIndex = tournamentSolutionGAsIndexes[0];
                    for (int j = 0; j < tournamentSolutionGAsIndexes.Length; j++)
                    {
                        if (fitnesses[tournamentSolutionGAsIndexes[j]] > bestFitness)
                        {
                            bestFitness = fitnesses[tournamentSolutionGAsIndexes[j]];
                            bestFitnessIndex = tournamentSolutionGAsIndexes[j];
                        }
                    }
                    if (!parents.Contains(bestFitnessIndex)) parents.Add(bestFitnessIndex);
                }
                return parents;
            }

            /*
             * 
             * Crossover operations block
             * 
             * */
            private SolutionGA[] Crossover(int[] parents)
            {
                if (parents.Length < 2) return null;
                List<SolutionGA> Childs = new List<SolutionGA>();
                int randomParentOne = 0;
                int randomParentTwo = 0;
                int randomPositionOne = 0;
                int randomPositionTwo = 0;
                int k = 0;
                for (int i = 0; i < Population.Length; i++)
                {
                    int counter = 0;
                    while (randomParentOne == randomParentTwo)
                    {
                        randomPositionOne = rndm.Next(parents.Length);
                        randomPositionTwo = rndm.Next(parents.Length);
                        randomParentOne = parents[randomPositionOne];
                        randomParentTwo = parents[randomPositionTwo];
                        if (counter > 100) break;
                        counter++;
                    }
                    Childs.Add(SolutionGA.Crossover(Population[randomParentOne],
                                                            Population[randomParentTwo]));
                    k = k < parents.Length - 1 ? k + 1 : 0;
                }

                return Childs.ToArray();
            }

            /*
             * 
             * Post-selection operations block
             * 
             * */
            private void PostSelection(SolutionGA[] Childs)
            {
                if (current.UseElitism)
                {
                    double[] fitnesses = new double[Population.Length];
                    int[] indexes = new int[1];
                    for (int i = 0; i < Population.Length; i++)
                    {
                        fitnesses[i] = Population[i].Fitness;
                    }
                    Various.OrderByDesc(ref fitnesses, ref indexes, true);
                    Population[0] = Population[indexes[0]];
                }
                switch (current.PostSelectionMode)
                {
                    case Settings.PostSelectionBest:
                        PostSelectionBest(Childs);
                        break;
                    case Settings.PostSelectionHalfOldHalfNew:
                        PostSelectionHalfOldHalfNew(Childs);
                        break;
                    case Settings.PostSelectionNew:
                        PostSelectionNew(Childs);
                        break;
                }
            }

            private void PostSelectionBest(SolutionGA[] Childs)
            {
                double[] mixedFitnesses = new double[Childs.Length + Population.Length];
                SolutionGA[] mixedSolutionGAs = new SolutionGA[Childs.Length + Population.Length];
                for (int i = 0; i < Childs.Length; i++)
                {
                    mixedFitnesses[i] = Childs[i].Fitness;
                    mixedSolutionGAs[i] = Childs[i];
                }
                for (int i = Childs.Length; i < mixedFitnesses.Length; i++)
                {
                    mixedFitnesses[i] = Population[i - Childs.Length].Fitness;
                    mixedSolutionGAs[i] = Population[i - Childs.Length];
                }
                int[] indexesOfMixedSolutionGAs = new int[1];
                Various.OrderByDesc(ref mixedFitnesses, ref indexesOfMixedSolutionGAs, true);
                for (int i = current.UseElitism ? 1 : 0; i < Population.Length; i++)
                {
                    Population[i] = mixedSolutionGAs[indexesOfMixedSolutionGAs[i - (current.UseElitism ? 1 : 0)]];
                }
            }

            private void PostSelectionHalfOldHalfNew(SolutionGA[] Childs)
            {
                List<double> parents = new List<double>();
                List<double> childs = new List<double>();

                for (int i = 0; i < Childs.Length; i++)
                {
                    childs.Add(Childs[i].Fitness);
                }
                for (int i = 0; i < Population.Length; i++)
                {
                    parents.Add(Population[i].Fitness);
                }
                int[] indexesParents = new int[1];
                int[] indexesChilds = new int[1];
                double[] parentsFitnesess = parents.ToArray();
                double[] childsFitnesess = childs.ToArray();
                Various.OrderByDesc(ref parentsFitnesess, ref indexesParents, true);
                Various.OrderByDesc(ref childsFitnesess, ref indexesChilds, true);
                for (int i = current.UseElitism ? 1 : 0; i < Population.Length / 2; i++)
                {
                    Population[i] = Childs[indexesChilds[i - (current.UseElitism ? 1 : 0)]];
                }
                for (int i = Population.Length / 2; i < Population.Length; i++)
                {
                    Population[i] = Population[indexesParents[i - Population.Length / 2]];
                }
            }

            private void PostSelectionNew(SolutionGA[] Childs)
            {
                List<double> childs = new List<double>();

                for (int i = 0; i < Childs.Length; i++)
                {
                    childs.Add(Childs[i].Fitness);
                }
                int[] indexesChilds = new int[1];
                double[] childsFitnesess = childs.ToArray();
                Various.OrderByDesc(ref childsFitnesess, ref indexesChilds, true);
                for (int i = current.UseElitism ? 1 : 0; i < Population.Length; i++)
                {
                    Population[i] = Childs[indexesChilds[i - (current.UseElitism ? 1 : 0)]];
                }
            }

            /*
             * 
             * Post-optimization operations block
             * 
             * */
            private void PostOptimization()
            {
                switch (current.PostOptimizationMode)
                {
                    case Settings.PostOptimizationGradualCorrection:
                        PostOptimizationGradualCorrection();
                        break;
                    case Settings.PostOptimizationOneStepCorrection:
                        PostOptimizationOneStepCorrection();
                        break;
                }
            }

            private void PostOptimizationGradualCorrection()
            {
                Correction.GradualCorrectionMethod method = new Correction.GradualCorrectionMethod(task);
                List<Solution> corrected = method.correctSolutions(new List<Solution>(Population));
                Logging_ObjectiveFunctionCalculations += method.calculations;

                for (int i = 0; i < Population.Length; i++)
                {
                    if (i >= corrected.Count) break;
                    Population[Population.Length - i - 1] = (SolutionGA)corrected[i];
                    Population[Population.Length - i - 1].SetFitness();
                }
            }

            private void PostOptimizationOneStepCorrection()
            {
                Correction.OneStepCorrectionMethod method = new Correction.OneStepCorrectionMethod(task);
                List<Solution> corrected = method.correctSolutions(new List<Solution>(Population));
                Logging_ObjectiveFunctionCalculations += method.calculations;

                for (int i = 0; i < Population.Length; i++)
                {
                    if (i >= corrected.Count) break;
                    Population[Population.Length - i - 1] = (SolutionGA)corrected[i];
                    Population[Population.Length - i - 1].SetFitness();
                }
            }
        }

        public class SolutionGA : Solution
        {
            Process parent;

            public bool[][] ParametersBoolean;
            public double this[int index]
            {
                get
                {
                    return DecodeVector(ParametersBoolean[index]);
                }
                set
                {
                    Parameters[index] = value;
                    ParametersBoolean[index] = CodeVector(value);
                }
            }
            public double[] ParametersDouble
            {
                get
                {
                    double[] array = new double[ParametersBoolean.Length];
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = DecodeVector(ParametersBoolean[i]);
                    }
                    return array;
                }
                set
                {
                    for (int i = 0; i < ParametersBoolean.Length; i++)
                    {
                        ParametersBoolean[i] = CodeVector(value[i]);
                        Parameters[i] = value[i];
                    }
                }
            }

            double _fitness;
            double _cleanFitness;
            double _value;

            public SolutionGA(Process parent, bool withRandomValues = false)
            {
                this.parent = parent;
                this.task = parent.task;
                ParametersBoolean = new bool[parent.task.Target.Parameters.Length][];
                Parameters = new double[ParametersBoolean.Length];
                if (withRandomValues)
                {
                    for (int i = 0; i < ParametersBoolean.Length; i++)
                    {
                        double l = parent.current.RightBorderOfFirstPopulationInterval - parent.current.LeftBorderOfFirstPopulationInterval;
                        this[i] = rndm.NextDouble() * l + parent.current.LeftBorderOfFirstPopulationInterval;
                    }
                }
                else
                {
                    for (int i = 0; i < ParametersBoolean.Length; i++)
                    {
                        ParametersBoolean[i] = new bool[parent.current.BitCount];
                    }
                }
            }

            public SolutionGA(SolutionGA s)
            {
                this.parent = s.parent;
                this.task = s.task;
                ParametersBoolean = new bool[parent.task.Target.Parameters.Length][];
                Parameters = new double[ParametersBoolean.Length];
                for (int i = 0; i < ParametersBoolean.Length; i++)
                {
                    this[i] = s[i];
                }
            }

            public void SetFitness()
            {
                double[] fitnessArray = FitnessCalculate(this);
                _fitness = fitnessArray[2];
                _cleanFitness = fitnessArray[1];
                _value = fitnessArray[0];
            }

            /*
             * 
             * Fitness calculation block
             * 
             * */

            static public double[] FitnessCalculate(SolutionGA s)
            {
                double[] x = s.ParametersDouble;
                s.parent.task.Target.Parameters = x;

                s.parent.Logging_ObjectiveFunctionCalculations++;

                double fitness = s.parent.task.Target.TargetFunction();

                double[] fitnessArray = new double[3] { fitness, fitness, fitness }; // value, clean, used

                fitness = 1 / (fitness + 1 - s.parent.current.ObjectiveFunctionMinValue);

                fitnessArray[1] = fitness;

                fitnessArray[2] = fitness;

                if (!object.Equals(s.parent.task.Constraints, null))
                {
                    switch (s.parent.current.PenaltyMode)
                    {
                        case Settings.PenaltyEmpty:
                            break;
                        case Settings.PenaltyDynamic:
                            fitnessArray[2] -= s.PenaltyDynamic(x);
                            break;
                        case Settings.PenaltyConstraint:
                            fitnessArray[2] = s.Violation;
                            break;
                        case Settings.PenaltyAdaptive:
                            fitnessArray[2] -= s.PenaltyAdaptive(x);
                            break;
                    }
                }

                return fitnessArray;
            }

            public double Fitness
            {
                get
                {
                    return _fitness;
                }
            }

            public double CleanFitness
            {
                get
                {
                    return _cleanFitness;
                }
            }

            public double Value
            {
                get
                {
                    return _value;
                }
            }

            double PenaltyStatic(double[] x)
            {
                int numberOfNotViolated = 0;
                for (int i = 0; i < parent.task.Constraints.Length; i++)
                {
                    parent.task.Constraints[i].Parameters = x;
                    double value = parent.task.Constraints[i].TargetFunction();
                    if (parent.task.IsEquality[i])
                    {
                        if (Math.Abs(value) <= parent.current.Precision) numberOfNotViolated++;
                    }
                    else
                    {
                        if (value <= parent.current.Precision) numberOfNotViolated++;
                    }
                }
                double bigValue = parent.current.RightBorderOfFirstPopulationInterval * 10;
                // Static penalty is here:
                return bigValue - numberOfNotViolated * bigValue / parent.task.Constraints.Length;
            }

            double PenaltyNew(double[] x)
            {
                double f = 0;
                ParametersDouble = x;
                if (IsFeasible)
                {
                    parent.task.Target.Parameters = x;
                    f = parent.task.Target.TargetFunction();
                    f += 10000;
                }
                else
                {
                    f = -Violation * Violation;
                }
                return f;
            }

            double PenaltyDynamic(double[] x)
            {
                double penalty = 0;
                // Dynamic method needs parameters
                double C, alpha, beta;
                C = 0.5;
                alpha = 2;
                beta = 2;
                double svc = 0;
                for (int i = 0; i < parent.task.Constraints.Length; i++)
                {
                    parent.task.Constraints[i].Parameters = x;
                    double value = parent.task.Constraints[i].TargetFunction();
                    if (parent.task.IsEquality[i])
                    {
                        if (Math.Abs(value) > parent.current.Precision)
                        {
                            svc += Math.Abs(value);
                        }
                    }
                    else
                    {
                        if (value < parent.current.Precision)
                        {
                            svc += Math.Pow(Math.Abs(value), beta);
                        }
                    }
                }
                penalty = Math.Pow(C * parent.CurrentIteration, alpha) + svc;
                return penalty;
            }

            double PenaltyAdaptive(double[] x)
            {
                double penalty = 0;
                double b1, b2;
                b1 = 2;
                b2 = 2;

                if (parent.ViolationsCount < parent.Population.Length * 0.05)
                {
                    penalty = PenaltyDynamic(x) * (1 / b1);
                }
                else if (parent.ViolationsCount < parent.Population.Length * 0.95)
                {
                    penalty = PenaltyDynamic(x);
                }
                else
                {
                    penalty = PenaltyDynamic(x) * b2;
                }
                return penalty;
            }

            double PenaltyLemongeBarbosa(double[] x)
            {
                double penalty = 0;
                double k = 0;
                if (IsFeasible) return 0;
                for (int i = 0; i < parent.task.Constraints.Length; i++)
                {
                    parent.task.Constraints[i].Parameters = x;
                    double value = parent.task.Constraints[i].TargetFunction();
                    k = parent.AverageFitness * parent.AverageViolations[i] / parent.AverageViolationsSquaredSum;
                    penalty += k * Math.Abs(value);
                }
                return penalty;
            }

            /*
             * 
             * Boolean strings operations block starts
             * 
             * */

            double DecodeVector(bool[] v)
            {
                bool[] vector = parent.current.UseGreyCode ? GreyDecode(v) : v;
                int val = 0;
                for (int i = 0; i < parent.current.BitCount - (parent.current.UsePositiveOnly ? 0 : 1); i++)
                {
                    if (vector[i] == true) val += (int)Math.Pow(2.0, i);
                }
                if (!vector[parent.current.BitCount - 1] && !parent.current.UsePositiveOnly) val = -val;
                double value = val * parent.current.Precision;
                return value;
            }

            bool[] CodeVector(double value)
            {
                bool[] vect = new bool[parent.current.BitCount];
                int val = (int)Math.Round(value / parent.current.Precision);
                vect = new bool[parent.current.BitCount];
                if (val > 0 && !parent.current.UsePositiveOnly) vect[parent.current.BitCount - 1] = true;
                val = Math.Abs(val);
                for (int i = 0; i < parent.current.BitCount - (parent.current.UsePositiveOnly ? 0 : 1); i++)
                {
                    int rest = val % 2;
                    val = val / 2;
                    bool gene = (rest == 1);
                    vect[i] = gene;
                }
                vect = parent.current.UseGreyCode ? GreyCode(vect) : vect;
                return vect;
            }

            bool[] GreyCode(bool[] vector)
            {
                bool[] result = new bool[parent.current.BitCount];
                vector.CopyTo(result, 0);
                if (!parent.current.UseGreyCode) return result;
                for (int i = 0; i < parent.current.BitCount - 1; i++)
                {
                    if (result[i] && result[i + 1])
                    {
                        result[i] = false;
                        result[i + 1] = true;
                    }
                    else if (result[i] && !result[i + 1])
                    {
                        result[i] = true;
                        result[i + 1] = false;
                    }
                    else if (!result[i] && result[i + 1])
                    {
                        result[i] = true;
                        result[i + 1] = true;
                    }
                    else if (!result[i] && !result[i + 1])
                    {
                        result[i] = false;
                        result[i + 1] = false;
                    }
                }
                return result;
            }

            bool[] GreyDecode(bool[] vector)
            {
                bool[] result = new bool[parent.current.BitCount];
                vector.CopyTo(result, 0);
                for (int i = (parent.current.BitCount - 1); i > 0; i--)
                {
                    if (result[i] && result[i - 1])
                    {
                        result[i] = true;
                        result[i - 1] = false;
                    }
                    else if (!result[i] && result[i - 1])
                    {
                        result[i] = false;
                        result[i - 1] = true;
                    }
                    else if (result[i] && !result[i - 1])
                    {
                        result[i] = true;
                        result[i - 1] = true;
                    }
                    else if (!result[i] && !result[i - 1])
                    {
                        result[i] = false;
                        result[i - 1] = false;
                    }
                }
                return result;
            }

            /*
             * 
             * Mutation operations block starts
             * 
             * */

            public void Mutate()
            {
                bool changed = false;
                switch (parent.current.MutationMode)
                {
                    case Settings.MutationBoolean:
                        changed = MutationBoolean();
                        break;
                    case Settings.MutationReal:
                        changed = MutationReal();
                        break;
                }
            }

            bool MutationBoolean()
            {
                bool changed = false;
                for (int i = 0; i < ParametersBoolean.Length; i++)
                {
                    for (int j = 0; j < ParametersBoolean[i].Length; j++)
                    {
                        if (rndm.NextDouble() <= parent.current.MutationCoefficient)
                        {
                            ParametersBoolean[i][j] = !ParametersBoolean[i][j];
                            changed = true;
                        }
                    }
                }
                return changed;
            }

            bool MutationReal()
            {
                double k = Math.Log(parent.current.IterationsMaxNumber / (parent.CurrentIteration + 1));
                k /= Math.Log(parent.current.IterationsMaxNumber);

                for (int j = 0; j < ParametersBoolean.Length; j++)
                {
                    double l = parent.current.RightBorderOfFirstPopulationInterval - parent.current.LeftBorderOfFirstPopulationInterval;
                    double r = rndm.NextDouble() * l + parent.current.LeftBorderOfFirstPopulationInterval;
                    this[j] += (1 + parent.current.MutationCoefficient) * (2 * r - 1) * k;
                }

                return true;
            }

            /*
             * 
             * Crossover operations block starts
             * 
             * */

            static public SolutionGA Crossover(SolutionGA s1, SolutionGA s2)
            {
                //if (!object.Equals(s1.parent, s2.parent)) throw new Exception("GA crossover exception: parent processes mismatch.");
                SolutionGA sNew = null;
                switch (s1.parent.current.CrossoverMode)
                {
                    case Settings.CrossoverReal:
                        sNew = CrossoverReal(s1, s2);
                        break;
                    case Settings.CrossoverTwoPoints:
                        sNew = CrossoverTwoPoints(s1, s2);
                        break;
                    case Settings.CrossoverUniform:
                        sNew = CrossoverUniform(s1, s2);
                        break;
                }
                if (object.Equals(sNew, null)) sNew = CrossoverTwoPoints(s1, s2);
                return sNew;
            }

            static private SolutionGA CrossoverTwoPoints(SolutionGA s1, SolutionGA s2)
            {
                SolutionGA sNew = new SolutionGA(s1.parent);
                int positionOne = 0;
                int positionTwo = 0;
                for (int i = 0; i < s1.ParametersBoolean.Length; i++)
                {
                    int counter = 0;
                    while (positionOne == positionTwo)
                    {
                        if (counter > 100) break;
                        counter++;
                        positionOne = rndm.Next(0, s1.parent.current.BitCount);
                        positionTwo = rndm.Next(0, s1.parent.current.BitCount);
                    }
                    for (int j = 0; j < positionOne; j++)
                    {
                        sNew.ParametersBoolean[i][j] = s1.ParametersBoolean[i][j];
                    }
                    for (int j = positionOne; j < positionTwo; j++)
                    {
                        if (j == s1.parent.current.BitCount) j = 0;
                        sNew.ParametersBoolean[i][j] = s2.ParametersBoolean[i][j];
                    }
                    if (positionTwo > positionOne)
                    {
                        for (int j = positionTwo; j < s1.parent.current.BitCount; j++)
                        {
                            sNew.ParametersBoolean[i][j] = s1.ParametersBoolean[i][j];
                        }
                    }
                }
                return sNew;
            }

            static private SolutionGA CrossoverUniform(SolutionGA s1, SolutionGA s2)
            {
                SolutionGA sNew = new SolutionGA(s1.parent);
                for (int i = 0; i < sNew.ParametersBoolean.Length; i++)
                {
                    for (int j = 0; j < sNew.ParametersBoolean[i].Length; j++)
                    {
                        if (rndm.NextDouble() > 0.5)
                        {
                            sNew.ParametersBoolean[i][j] = s1.ParametersBoolean[i][j];
                        }
                        else
                        {
                            sNew.ParametersBoolean[i][j] = s2.ParametersBoolean[i][j];
                        }
                    }
                }
                return sNew;
            }

            static private SolutionGA CrossoverReal(SolutionGA s1, SolutionGA s2)
            {
                SolutionGA sNew = new SolutionGA(s1.parent);
                for (int i = 0; i < sNew.ParametersBoolean.Length; i++)
                {
                    sNew[i] = (s1[i] + s2[i]) / 2;
                }
                return sNew;
            }
        }
    }
}
