namespace SignalFx.Tracing.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [Duck(Name = "_publicStaticReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }

        [Duck(Name = "_internalStaticReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }

        [Duck(Name = "_protectedStaticReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }

        [Duck(Name = "_privateStaticReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }

        // *

        [Duck(Name = "_publicStaticSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PublicStaticSelfTypeField { get; set; }

        [Duck(Name = "_internalStaticSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject InternalStaticSelfTypeField { get; set; }

        [Duck(Name = "_protectedStaticSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }

        [Duck(Name = "_privateStaticSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PrivateStaticSelfTypeField { get; set; }

        // *

        [Duck(Name = "_publicReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PublicReadonlySelfTypeField { get; }

        [Duck(Name = "_internalReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject InternalReadonlySelfTypeField { get; }

        [Duck(Name = "_protectedReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject ProtectedReadonlySelfTypeField { get; }

        [Duck(Name = "_privateReadonlySelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PrivateReadonlySelfTypeField { get; }

        // *

        [Duck(Name = "_publicSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PublicSelfTypeField { get; set; }

        [Duck(Name = "_internalSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject InternalSelfTypeField { get; set; }

        [Duck(Name = "_protectedSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject ProtectedSelfTypeField { get; set; }

        [Duck(Name = "_privateSelfTypeField", Kind = DuckKind.Field)]
        public abstract IDummyFieldObject PrivateSelfTypeField { get; set; }
    }
}
