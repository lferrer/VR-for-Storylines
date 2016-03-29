using System;


public class TextGenerator  {

    private static string[] words =
    {
        "account",
        "accounting",
        "acquisition",
        "agenda",
        "agreement",
        "asset",
        "balance sheet",
        "board of directors",
        "bonus",
        "branding",
        "budget",
        "business",
        "business plan",
        "capital",
        "cash flow statement",
        "CBA",
        "CEO",
        "c/o",
        "commercial",
        "company",
        "competitor",
        "conference call",
        "consumer",
        "contract",
        "cost of sales",
        "credit",
        "customer service department",
        "CV",
        "deadline",
        "disruption",
        "downsizing",
        "end user",
        "entrepreneur",
        "equity",
        "exchange rate",
        "grant",
        "headquarters",
        "Human Resources",
        "income statement",
        "industry",
        "information technology",
        "interest",
        "inventory",
        "investment",
        "launch",
        "liability",
        "lien",
        "loan",
        "logistics",
        "management",
        "manager",
        "margin",
        "market",
        "marketing",
        "meeting",
        "merger",
        "monetization",
        "multitask",
        "networking",
        "non profit organization",
        "objective",
        "operations",
        "opportunity cost",
        "outsourcing",
        "overhead",
        "partnership",
        "party",
        "personnel",
        "platform",
        "point of sale",
        "sales department",
        "scalable",
        "shareholder",
        "social media",
        "sponsor",
        "staff",
        "stakeholder",
        "startup",
        "status report",
        "strategy",
        "supply chain",
        "telecommuting",
        "terms",
        "trademark",
        "transaction",
        "Venture Capital",
        "viral marketing",
        "web 2.0",
        "wholesale"
    };

    public static string[] TopNWords(int n, int k)
    {
        string[] res = new string[n];
        Random rnd = new Random(k);
        for (int i = 0; i < n; i++)
        {
            int index = rnd.Next(0, words.Length);
            res[i] = words[index];
        }
        return res;
    }
}
