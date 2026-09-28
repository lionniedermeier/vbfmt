Module Calculations
    Public Function BreaksParametersBeforeTypeParameters(Of TValue, CValue, KValue)(
        paramA As Integer, paramB As Integer, genericParam As TValue
    ) As (ParamA As Integer, ParamB As Integer, GenericValue As TValue)
        Return (paramA, paramB, genericParam)
    End Function

    Public Function BreaksParametersBeforeGenericReturnType(
        paramA As Integer, paramB As Integer
    ) As Tuple(Of LongTypeName___________, LongTypeName___________, LongTypeName___________)
        Return Tuple.Create(0, 0, 0)
    End Function

    Public Function BreaksTupleReturnTypeOneElementPerLine(
        paramA As Integer, paramB As Integer
    ) As (
        ParamA As VeryLongTypeName________________________,
        ParamB As VeryLongTypeName________________________,
        GenericValue As VeryLongTypeName________________________
    )
        Return Nothing
    End Function

    Public Function BreaksEveryPartOfAnOverlongSignature(
        Of TValue_____________,
        CValue_____________,
        KValue_____________,
        DValue_____________
    )(
        paramA As Integer, paramB As Integer, genericParam As TValue_____________
    ) As Dictionary(
        Of CValue______________________________________________,
        KValue______________________________________________
    )
        Return Nothing
    End Function

    Public Function BreaksGenericReturnTypeWithoutParameters(Of T)() As Dictionary(
        Of LongTypeName___________,
        LongTypeName___________
    )
        Return Nothing
    End Function

    Public Delegate Function LongDelegateSignature(Of TValue, CValue, KValue)(
        paramA As Integer, paramB As Integer
    ) As Tuple(Of TValue, CValue, KValue)
End Module

Public Interface ICalculator
    Function LongInterfaceMethodSignature(Of TValue, CValue, KValue)(
        paramA As Integer, paramB As Integer
    ) As Tuple(Of TValue, CValue, KValue)
End Interface
