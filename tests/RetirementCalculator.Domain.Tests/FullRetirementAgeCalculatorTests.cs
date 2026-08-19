using RetirementCalculator.Domain.Services;

namespace RetirementCalculator.Domain.Tests;

[TestClass]
public sealed class FullRetirementAgeCalculatorTests
{
    [TestMethod]
    [DataRow(1936, 65, 0)]
    [DataRow(1937, 65, 0)] // last year of the pre-transition schedule
    [DataRow(1938, 65, 2)] // first transition year
    [DataRow(1939, 65, 4)]
    [DataRow(1940, 65, 6)]
    [DataRow(1941, 65, 8)]
    [DataRow(1942, 65, 10)] // last transition year before flat 66
    [DataRow(1943, 66, 0)] // start of flat 66 period
    [DataRow(1954, 66, 0)] // last year of flat 66 period
    [DataRow(1955, 66, 2)] // first transition year toward 67
    [DataRow(1956, 66, 4)]
    [DataRow(1957, 66, 6)]
    [DataRow(1958, 66, 8)]
    [DataRow(1959, 66, 10)] // last transition year before flat 67
    [DataRow(1960, 67, 0)] // start of flat 67 period
    [DataRow(1975, 67, 0)]
    public void Calculate_ReturnsExpectedFullRetirementAge(int birthYear, int expectedYears, int expectedMonths)
    {
        var fra = FullRetirementAgeCalculator.Calculate(birthYear);

        Assert.AreEqual(expectedYears, fra.Years);
        Assert.AreEqual(expectedMonths, fra.Months);
    }
}
