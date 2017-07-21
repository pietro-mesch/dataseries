Imports System
Imports System.Collections.Generic
Imports System.Linq

Namespace Dataseries

    Public MustInherit Class DateSeries(Of T)
#Region "DECLARATIONS"
        Protected _p As SortedDictionary(Of Date, T)
        Protected _t0 As Date
        Protected _tN As Date

#End Region

#Region "CONSTRUCTORS"
        Protected Sub New(fdat As Date, ldat As Date, Optional default_value As T = Nothing)
            If fdat = Nothing OrElse ldat = Nothing Then
                Throw New ArgumentNullException("Timeseries boundaries cannot be null dates")
            ElseIf fdat > ldat Then
                Throw New ArgumentException("The first date cannot be after the last")
            End If

            _p = New SortedDictionary(Of Date, T)

            'ADD THE FIRST TWO POINTS TO THE DICTIONARY
            _t0 = fdat
            _tN = ldat
            _p(_t0) = default_value
            _p(_tN) = default_value

        End Sub

#End Region

#Region "PUBLIC METHODS"
        Public Sub ReadIntervals(values As T(), fdat As Date(), ldat As Date(), prio() As IComparable)
            Dim i, j, this, that As Integer
            Dim use_this As Boolean
            Dim update_those As List(Of Date)

            Dim nI As Integer = values.Length
            If fdat.Length <> nI OrElse ldat.Length <> nI Then
                Throw New ArgumentException()
            End If

            '1: SORT INTERVALS BY PRIORITY
            Dim idx_sorted(nI - 1) As Integer
            For i = 0 To nI - 1
                idx_sorted(i) = i
            Next
            'the priority array is consumed, losing correspondence with indices of value, fdat, ldat
            Array.Sort(prio, idx_sorted)
            'the information is now contained in this array
            idx_sorted = idx_sorted.Reverse.ToArray

            '2: CONSUME INTERVALS IN ORDER OF DECREASING PRIORITY
            For i = 0 To nI - 1
                'THIS is the interval being consumed
                this = idx_sorted(i)
                use_this = True
                update_those = New List(Of Date)

                For j = 0 To i - 1

                    'THAT is each of the intervals already consumed and used
                    that = idx_sorted(j)
                    If that = -1 Then Continue For

                    'CHECK BEGINNING
                    If fdat(this) = NullDate OrElse fdat(this) < _t0 Then
                        fdat(this) = _t0
                    End If
                    'if THIS start date is shadowed by THAT more important interval, move THIS start to the end of THAT
                    If fdat(this) >= fdat(that) AndAlso fdat(this) < ldat(that) Then
                        fdat(this) = ldat(that)
                        'if moving the start date reduces THIS to zero or THIS was fully shadowed by THAT, do not use THIS
                        If fdat(this) >= ldat(this) Then
                            use_this = False
                            idx_sorted(i) = -1
                            Exit For
                        End If
                    End If

                    'CHECK END
                    If ldat(this) = NullDate OrElse ldat(this) > _tN Then
                        ldat(this) = _tN
                    End If
                    'if THIS end date is shadowed by THAT more important interval, move THIS end to the beginning of THAT
                    If ldat(this) >= fdat(that) AndAlso ldat(this) <= ldat(that) Then
                        ldat(this) = fdat(that)
                        'if moving the end date reduces THIS to zero, do not use THIS
                        If fdat(this) = ldat(this) Then
                            use_this = False
                            idx_sorted(i) = -1
                            Exit For
                        End If
                    End If

                    'CHECK SPLITS
                    'if THIS is split in two by THAT
                    If ldat(that) > fdat(this) AndAlso ldat(that) < ldat(this) Then
                        'and the value at the end of THAT is still the default value, it should be changed to fall onto THIS value
                        update_those.Add(ldat(that))
                    End If

                Next

                '3: INTERVAL IS USED IF NOT SHADOWED BY THE PREVIOUS ONES
                If use_this Then
                    'ADD OR UPDATE RELEVANT POINTS

                    '1: the final instant must be inserted first so GetValue can return the underlying current value
                    'the last instant of the timeseries may be overwritten 
                    '(happens once at most, if the value was not previously written by THAT, which would have shadowed THIS already)
                    _p(ldat(this)) = If(Not ldat(this) = _tN, GetValue(ldat(this)), values(this))

                    '2: any of THOSE higher priority intervals whose end comes to fall into THIS will see its enddate value updated
                    For Each dt As Date In update_those
                        _p(dt) = values(this)
                    Next

                    '3: finally, the initial instant is written too
                    _p(fdat(this)) = values(this)
                End If

            Next

        End Sub
#End Region

#Region "PUBLIC PROPERTIES"
        Default Public ReadOnly Property GetValue(instant As Date) As T
            Get
                If instant < _t0 Then
                    Throw New System.ArgumentOutOfRangeException("The selected date falls before the start of the timeseries.")
                ElseIf instant > _tN Then
                    Throw New System.ArgumentOutOfRangeException("The selected date falls after the end of the timeseries.")
                Else

                    Return _p.Values(IndexOfIntervalContainingInstant(instant))

                End If
            End Get
        End Property

        ''' <summary>
        ''' Returns the Mode Value of the interval between from_date and to_date.
        ''' </summary>
        ''' <param name="from_date"></param>
        ''' <param name="to_date"></param>
        ''' <returns>The longest occurring value during the selected interval.</returns>
        Public Function GetIntervalModeValue(from_date As Date, to_date As Date) As T
            Dim i As Integer

            If from_date = Nothing OrElse to_date = Nothing Then
                Throw New ArgumentNullException("Interval boundaries cannot be null dates.")
            ElseIf from_date > to_date Then
                Throw New ArgumentException("Interval start date cannot be later than interval end date.")
            End If

            If from_date < _t0 OrElse to_date > _tN Then
                Throw New System.ArgumentOutOfRangeException("The selected interval falls beyond the domain of the timeseries.")
            Else

                Dim from_interval As Integer = IndexOfIntervalContainingInstant(from_date)
                Dim to_interval As Integer = IndexOfIntervalContainingInstant(to_date)

                '1: the simple case
                If from_interval = to_interval Then
                    Return _p.Values(from_interval)
                End If

                '2: otherwise return the value that occurs for the majority of time during the selected interval

                'accumulate time for each value taken by the series over the interval
                'a dictionary is used so that equal values are accumulated together
                Dim values_durations As New Dictionary(Of T, Double)
                values_durations.Add(_p.Values(from_interval), TimeToNextInterval(from_date))
                For i = from_interval + 1 To to_interval - 1
                    values_durations.Item(_p.Values(i)) = DurationOfInterval(i) + If(values_durations.Keys.Contains(_p.Values(i)), values_durations(_p.Values(i)), 0)
                Next
                values_durations.Item(_p.Values(to_interval)) = TimeSincePreviousInterval(to_date) + If(values_durations.Keys.Contains(_p.Values(to_interval)), values_durations(_p.Values(to_interval)), 0)

                'find the index in the dictionary of the longest running value
                Dim ret As Integer
                Dim longest_time As Double = -1
                For i = 0 To values_durations.Count - 1
                    If values_durations.Values(i) > longest_time Then
                        longest_time = values_durations.Values(i)
                        ret = i
                    End If
                Next

                'return the corresponding object/value
                Return values_durations.Keys(ret)

            End If

            Return Nothing
        End Function

#End Region

#Region "PRIVATE METHODS"
        Protected Function IndexOfIntervalContainingInstant(instant As Date) As Integer
            Dim i As Integer = 0
            While i < _p.Count AndAlso _p.Keys(i) <= instant
                i += 1
            End While

            Return i - 1
        End Function

        Protected Function DurationOfInterval(index As Integer) As Double
            Return (_p.Keys(index + 1) - _p.Keys(index)).TotalSeconds
        End Function

        Protected Function TimeToNextInterval(dt As Date) As Double
            Return (_p.Keys(IndexOfIntervalContainingInstant(dt) + 1) - dt).TotalSeconds
        End Function

        Protected Function TimeSincePreviousInterval(dt As Date) As Double
            Return (dt - _p.Keys(IndexOfIntervalContainingInstant(dt))).TotalSeconds
        End Function
#End Region

    End Class

    Public Class DateSeriesOfInteger
        Inherits DateSeries(Of Integer)
        Public Sub New(fdat As Date, ldat As Date, Optional default_value As Integer = Nothing)
            MyBase.New(fdat, ldat, default_value)
        End Sub

        ''' <summary>
        ''' Returns the Average Value during the selected interval.
        ''' </summary>
        ''' <param name="from_date"></param>
        ''' <param name="to_date"></param>
        ''' <returns></returns>
        Public Function GetIntervalAverageValue(from_date As Date, to_date As Date) As Integer
            If from_date = Nothing OrElse to_date = Nothing Then
                Throw New ArgumentNullException("Interval boundaries cannot be null dates.")
            ElseIf from_date > to_date Then
                Throw New ArgumentException("Interval start date cannot be later than interval end date.")
            End If

            If from_date < _t0 OrElse to_date > _tN Then
                Throw New System.ArgumentOutOfRangeException("The selected interval falls beyond the domain of the timeseries.")
            Else

                Dim from_interval As Integer = IndexOfIntervalContainingInstant(from_date)
                Dim to_interval As Integer = IndexOfIntervalContainingInstant(to_date)

                '1: the simple case
                If from_interval = to_interval Then
                    Return _p.Values(from_interval)
                End If

                '2: otherwise return the time weighted average
                Dim ret As Double
                Dim time As Double
                time = TimeToNextInterval(from_date)
                ret = _p.Values(from_interval) * TimeToNextInterval(from_date)
                For i As Integer = from_interval + 1 To to_interval - 1
                    time += DurationOfInterval(i)
                    ret += _p.Values(i) * DurationOfInterval(i)
                Next
                time += TimeSincePreviousInterval(to_date)
                ret += _p.Values(to_interval) * TimeSincePreviousInterval(to_date)

                Return CInt(Math.Round(ret / time))
            End If

            Return Nothing
        End Function

    End Class
End Namespace
