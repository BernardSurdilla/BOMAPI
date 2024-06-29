using UnitsNet;
namespace BillOfMaterialsAPI.Helpers
{
    public class IdFormat
    {
        public static string materialIdFormat = "MID";
        public static string materialIngredientIdFormat = "MIID";
        public static string pastryMaterialIdFormat = "PMID";
        public static string ingredientIdFormat = "IID";
        public static string pastryMaterialAddOnFormat = "PMAOID";
        public static string pastryMaterialSubVariantIdFormat = "SVID";
        public static string pastryMaterialSubVariantIngredientIdFormat = "SVIID";
        public static string pastryMaterialSubVariantAddOnFormat = "SVAOID";
        public static string logsIdFormat = "LOG";
        public static int idNumLength = 12;

        public static string IncrementId(string idStringBuffer, int idNumberLength, string idString)
        {
            int index = idString.IndexOf(idStringBuffer);
            string idNumeralsPart = (index < 0) ? idString : idString.Remove(index, idStringBuffer.Length);
            int idInt = Convert.ToInt32(idNumeralsPart);

            int newIdInt = idInt + 1;
            int numberOfNumerals = Convert.ToInt32(newIdInt.ToString()).ToString().Length;

            string newId = newIdInt.ToString();
            for (int i = 0; i < idNumberLength - numberOfNumerals; i++)
            {
                newId = "0" + newId;
            }
            newId = idStringBuffer + newId;
            return newId;
        }
    }
    public class Iterators
    {
        public static IEnumerable<DateTime> LoopThroughMonths(DateTime start, DateTime end)
        {
            DateTime startDate = new DateTime(start.Year, start.Month, 1);
            DateTime endDate = new DateTime(end.Year, end.Month, 1);

            for (DateTime i = startDate; i <= endDate; i.AddMonths(1)) yield return i;
        }
    }

    public class Page
    {
        public static int DefaultStartingPageNumber = 1;
        public static int DefaultNumberOfEntriesPerPage = 10;
    }

    public class ValidUnits
    {
        public static Dictionary<string, List<string>> ValidMeasurementUnits()
        {
            Dictionary<string, List<string>> response = new Dictionary<string, List<string>>();

            string[] validQuantities = ["Mass", "Volume"];
            foreach (string currentQuantity in validQuantities)
            {
                List<string> currentQuantityUnits = new List<string>();
                foreach (UnitInfo currentUnit in Quantity.ByName[currentQuantity].UnitInfos)
                {
                    currentQuantityUnits.Add(currentUnit.Name);
                }
                response.Add(currentQuantity, currentQuantityUnits);
            }
            response.Add("Count", new List<string> { "Piece" });

            return response;
        }
        public static bool IsSameQuantityUnit(string x, string y)
        {
            Dictionary<string, List<string>> response = new Dictionary<string, List<string>>();

            string[] validQuantities = ["Mass", "Volume", "Count"];
            foreach (string currentQuantity in validQuantities)
            {
                if (currentQuantity.Equals("Count") == false)
                {
                    bool doesXExistInCurrentQuantityUnit = false;
                    bool doesYExistInCurrentQuantityUnit = false;

                    foreach (UnitInfo currentUnit in Quantity.ByName[currentQuantity].UnitInfos)
                    {
                        if (currentUnit.Name.Equals(x)) { doesXExistInCurrentQuantityUnit = true; }
                        if (currentUnit.Name.Equals(y)) { doesYExistInCurrentQuantityUnit = true; }
                        if (doesXExistInCurrentQuantityUnit == true && doesYExistInCurrentQuantityUnit == true) { break; }
                    }
                    if (doesXExistInCurrentQuantityUnit == true && doesYExistInCurrentQuantityUnit == true) { return true; }
                }
                else
                {
                    string validMeasurement = "Piece";
                    if (x.Equals(validMeasurement) && y.Equals(validMeasurement)) { return true; }
                }
            }
            return false;
        }
        public static bool IsUnitValid(string x)
        {
            Dictionary<string, List<string>> validUnitList = ValidMeasurementUnits();

            foreach (string quantity in validUnitList.Keys) 
            {
                List<string> units = validUnitList[quantity];
                if (units.Contains(x)) { return true; }
                else { continue; }
            }
            return false;
        }
        public static string UnitQuantityMeasurement(string x)
        {
            Dictionary<string, List<string>> validUnitList = ValidMeasurementUnits();

            foreach (string quantity in validUnitList.Keys)
            {
                List<string> units = validUnitList[quantity];
                if (units.Contains(x)) { return quantity; }
                else { continue; }
            }
            return "";
        }
    }
    
}
