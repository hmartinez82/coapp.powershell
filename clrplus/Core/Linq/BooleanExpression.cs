//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Core.Linq {
    using System;
    using System.Linq.Expressions;

    public class BooleanExpression<T> : Filter<T> {
        public BooleanExpression(Filter<T> left, Filter<T> right, BooleanOperation op) {
            Left = left;
            Right = right;
            Operator = op;
        }

        private Filter<T> Left {get; set;}
        private Filter<T> Right {get; set;}
        private BooleanOperation Operator {get; set;}

        public override Expression<Func<T, bool>> Expression {
            get {
                var p = System.Linq.Expressions.Expression.Parameter(typeof (T), "arg");

                var invokeLeft = System.Linq.Expressions.Expression.Invoke(Left, p);
                var invokeRight = System.Linq.Expressions.Expression.Invoke(Right, p);
                BinaryExpression bin = null;
                switch (Operator) {
                    case BooleanOperation.And:
                        bin = System.Linq.Expressions.Expression.And(invokeLeft, invokeRight);
                        break;
                    case BooleanOperation.Or:
                        bin = System.Linq.Expressions.Expression.Or(invokeLeft, invokeRight);
                        break;
                    case BooleanOperation.Xor:
                        bin = System.Linq.Expressions.Expression.ExclusiveOr(invokeLeft, invokeRight);
                        break;
                }

                if (bin == null) {
                    throw new Exception("This should never happen");
                }

                return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(bin, p);
            }
        }
    }
}