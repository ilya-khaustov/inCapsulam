﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using inCapsulam.Optimization.Methods;

namespace inCapsulam.Optimization.Correction
{
    class GradualCorrectionMethod : CorrectionMethod
    {
        public GradualCorrectionMethod(Task t)
        {
            task = t;
        }

        public override List<Solution> correctSolutions(List<Solution> mixed)
        {
            List<Solution> bad = new List<Solution>();
            List<Solution> good = new List<Solution>();

            List<Solution> corrected = new List<Solution>();

            for (int i = 0; i < mixed.Count; i++)
            {
                if (violationOf(mixed[i].Parameters) > 0)
                {
                    bad.Add(mixed[i]);
                }
                else
                {
                    good.Add(mixed[i]);
                }
            }

            if (good.Count == 0) return new List<Solution>();

            bad.Sort(compareSolutions);

            while (bad.Count > 0)
            {
                for (int i = 0; i < good.Count; i++)
                {
                    if (Program.rndm.NextDouble() > task.ga_Settings.PostOptimizationCoefficient) continue;
                    corrected.Add(BitSearch((GeneticApproachMethod.SolutionGA)good[i],
                            (GeneticApproachMethod.SolutionGA)bad.First()));
                    good.Add(corrected.Last());
                    bad.RemoveAt(0);
                    if (bad.Count == 0) break;
                }
            }

            return corrected;
        }
    }
}
