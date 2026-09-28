Module SingleArgument
    Public Sub Bounds()
        Dim newNonZeroWidthTokenTriviaCount(
            triviaToMoveIndex - triviaToMoveCount - 1
        ) As Microsoft.CodeAnalysis.SyntaxTrivia
    End Sub

    Public Sub CallsWithOneArgument()
        Compute(
            triviaToMoveIndex - triviaToMoveCount - someOtherReallyLongOffsetVariableNameHereForPaddingPurposesX - 1
        )
        Process(
            candidate.
                ResolvePrimaryHolder().
                LoadLinkedAccount().
                FetchActiveSubscription().
                IsWithinTheRenewalGracePeriodRightNow()
        )
    End Sub

    Public Sub WrapsItsBracket()
        Register(
            Function(item) item.IsActive AndAlso
                item.HasValidSignature AndAlso
                item.RemainingTermInMonths > 0 AndAlso
                Not item.IsSuspended
        )
    End Sub

    Public Sub KeepsTypeArgumentGlued()
        Dim useSiteInfo = CompoundUseSiteInfo(Of ExtremelyLongAssemblySymbolTypeNameUsedOnlyForTestingPurposesHere).DiscardedDependencies
    End Sub

    Public Sub Empty()
        Foo()
    End Sub
End Module
