using Xunit;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class DateFieldTests
{
    [Fact]
    public void CalendarPreservesDatesSupportsClearingAndNotifiesBindings()
    {
        var field = new EditField(new("ExpiryDate", "Действует до (дд.мм.гггг)", false), "29.02.2028", []);
        Assert.True(field.IsDate);
        Assert.True(field.CanClearDate);
        Assert.Equal(new DateTime(2028, 2, 29), field.SelectedDate);
        var changed = new List<string?>();
        field.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        field.SelectedDate = new DateTime(2029, 3, 1);
        Assert.Equal("01.03.2029", field.Value);
        field.SelectedDate = null;
        Assert.Equal("", field.Value);
        Assert.Contains("SelectedDate", changed);
        Assert.Contains("Value", changed);
        Assert.False(new EditField(new("IssueDate", "Выдан"), "", []).CanClearDate);
        Assert.False(new EditField(new("DocumentNumber", "Номер"), "", []).IsDate);
    }
}
