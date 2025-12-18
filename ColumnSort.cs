using System;
using System.Collections;
using System.Windows.Forms;

/// <summary>
/// A custom comparer for sorting ListView columns, implementing the 'IComparer' interface.
/// </summary>
public class ListViewColumnSorter : IComparer
{
    /// <summary>
    /// Case-insensitive comparer object used for comparing strings.
    /// </summary>
    private readonly CaseInsensitiveComparer ObjectCompare;

    /// <summary>
    /// Initializes a new instance of the ListViewColumnSorter class and sets default sorting parameters.
    /// </summary>
    public ListViewColumnSorter()
    {
        // Default column index for sorting
        SortColumn = 0;

        // Default sorting order
        Order = SortOrder.Ascending;

        // Initialize the case-insensitive comparer
        ObjectCompare = new CaseInsensitiveComparer();
    }

    /// <summary>
    /// Compares two ListViewItem objects based on the specified column index and sorting order.
    /// </summary>
    /// <param name="x">First ListViewItem object to compare.</param>
    /// <param name="y">Second ListViewItem object to compare.</param>
    /// <returns>
    /// A signed integer indicating the relative values of x and y: 
    /// "0" if equal, a negative number if x is less than y, and a positive number if x is greater than y.
    /// </returns>
    public int Compare(object x, object y)
    {
        int compareResult;
        ListViewItem listviewX = (ListViewItem)x;
        ListViewItem listviewY = (ListViewItem)y;

        // Special handling for column 6 (Popularity ranking)
        if (SortColumn == 6)
        {
            string textX = listviewX.SubItems[SortColumn].Text;
            string textY = listviewY.SubItems[SortColumn].Text;

            // Extract numeric values from "#1", "#10", etc.
            // "-" represents unranked and should go to the end
            int rankX = int.MaxValue; // Default for unranked (-)
            int rankY = int.MaxValue;

            if (textX.StartsWith("#") && int.TryParse(textX.Substring(1), out int parsedX))
            {
                rankX = parsedX;
            }

            if (textY.StartsWith("#") && int.TryParse(textY.Substring(1), out int parsedY))
            {
                rankY = parsedY;
            }

            // Compare the numeric ranks
            compareResult = rankX.CompareTo(rankY);
        }
        // Special handling for column 5 (Size)
        else if (SortColumn == 5)
        {
            string textX = listviewX.SubItems[SortColumn].Text;
            string textY = listviewY.SubItems[SortColumn].Text;

            double sizeX = ParseSize(textX);
            double sizeY = ParseSize(textY);

            // Compare the numeric sizes
            compareResult = sizeX.CompareTo(sizeY);
        }
        else
        {
            // Default to string comparison for non-numeric columns
            compareResult = ObjectCompare.Compare(listviewX.SubItems[SortColumn].Text, listviewY.SubItems[SortColumn].Text);
        }

        // Determine the return value based on the specified sort order
        if (Order == SortOrder.Ascending)
        {
            return compareResult;
        }
        else if (Order == SortOrder.Descending)
        {
            return -compareResult;
        }
        else
        {
            return 0; // Indicate equality
        }
    }

    /// <summary>
    /// Parses a numeric value from a string for accurate numeric comparison.
    /// </summary>
    /// <param name="text">The string representation of the number.</param>
    /// <returns>The parsed integer value; returns 0 if parsing fails.</returns>
    private int ParseNumber(string text)
    {
        // Directly attempt to parse the string as an integer
        return int.TryParse(text, out int result) ? result : 0;
    }

    /// <summary>
    /// Parses size strings with units (GB/MB) and converts them to MB for comparison.
    /// </summary>
    /// <param name="sizeStr">Size string (e.g., "1.23 GB", "123 MB")</param>
    /// <returns>Size in MB as a double</returns>
    private double ParseSize(string sizeStr)
    {
        if (string.IsNullOrEmpty(sizeStr))
            return 0;

        // Remove whitespace
        sizeStr = sizeStr.Trim();

        // Handle new format: "1.23 GB" or "123 MB"
        if (sizeStr.EndsWith(" GB", StringComparison.OrdinalIgnoreCase))
        {
            string numPart = sizeStr.Substring(0, sizeStr.Length - 3).Trim();
            if (double.TryParse(numPart, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double gb))
            {
                return gb * 1024.0; // Convert GB to MB for consistent sorting
            }
        }
        else if (sizeStr.EndsWith(" MB", StringComparison.OrdinalIgnoreCase))
        {
            string numPart = sizeStr.Substring(0, sizeStr.Length - 3).Trim();
            if (double.TryParse(numPart, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mb))
            {
                return mb;
            }
        }

        // Fallback: try parsing as raw number
        if (double.TryParse(sizeStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double rawMb))
        {
            return rawMb;
        }

        return 0;
    }

    /// <summary>
    /// Gets or sets the index of the column to be sorted (default is '0').
    /// </summary>
    public int SortColumn { get; set; }

    /// <summary>
    /// Gets or sets the order of sorting (Ascending or Descending).
    /// </summary>
    public SortOrder Order { get; set; }
}
