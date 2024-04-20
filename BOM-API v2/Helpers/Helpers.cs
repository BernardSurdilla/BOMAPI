﻿namespace BillOfMaterialsAPI.Helpers
{
    public class IdFormat
    {
        public static string materialIdFormat = "MID";
        public static string materialIngredientIdFormat = "MIID";
        public static string ingredientIdFormat = "IID";
        public static string pastryMaterialIdFormat = "PMID";
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

    public class Page
    {
        public static int DefaultStartingPageNumber = 1;
        public static int DefaultNumberOfEntriesPerPage = 10;
    }
}
