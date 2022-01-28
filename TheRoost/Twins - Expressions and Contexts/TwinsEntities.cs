using System.Collections;
using System.Collections.Generic;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Core;
using SecretHistories.UI;
using NCalc;

namespace TheRoost.Twins.Entities
{
    public struct Funcine<T>
    {
        readonly Expression expression;
        readonly FucineRef[] references;
        public readonly string formula;

        public Funcine(string expression)
        {
            this.formula = expression;
            this.references = FuncineParser.LoadReferences(ref expression).ToArray();
            this.expression = new Expression(Expression.Compile(expression, false));
        }

        public T result
        {
            get
            {
                foreach (FucineRef reference in references)
                    expression.Parameters[reference.idInExpression] = reference.value;
                object result = expression.Evaluate();

                return (T)TheRoost.Beachcomber.Panimporter.ConvertValue(result, typeof(T));
            }
        }

        public bool isUndefined { get { return this.expression == null; } }

        public override string ToString() { return "'" + this.formula + "' = " + this.result; }
    }

    public struct FucineRef
    {
        public readonly string idInExpression;

        public readonly string targetPath;
        public readonly string targetElementId;
        public readonly Funcine<bool> filter;

        public int value
        {
            get
            {
                IEnumerable<Token> tokens = TokenContextManager.GetReferencedTokens(targetPath);

                if (this.filter.isUndefined == false)
                    tokens = TokenContextManager.FilterTokens(tokens, filter);

                AspectsDictionary aspects = new AspectsDictionary();
                foreach (Token token in tokens)
                    aspects.CombineAspects(token.GetAspects());

                return aspects.AspectValue(targetElementId);
            }
        }

        public FucineRef(string id, string targetElement, string path, Funcine<bool> filter)
        {
            this.idInExpression = id;
            this.targetElementId = targetElement;
            this.targetPath = path;
            this.filter = filter;
        }

        public bool Equals(FucineRef otherReference)
        {
            return otherReference.targetPath.Equals(this.targetPath) && otherReference.targetElementId == this.targetElementId && otherReference.filter.isUndefined && this.filter.isUndefined;
        }
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IWeirdSpecEntity
    {
        [FucineValue(false)]
        public bool Additive { get; set; }
        [FucineStruct("1")]
        public Funcine<int> Level { get; set; }
        [FucineValue(ValidateAsElementId = true, DefaultValue = null)]
        public string Mutate { get; set; }

        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void WeirdSpec(Hashtable data)
        {
            if (Mutate == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Mutate = key.ToString();
                    this.Level = new Funcine<int>(UnknownProperties[key].ToString());
                    break;
                }
                UnknownProperties.Remove(Mutate);
            }
        }
    }
}
