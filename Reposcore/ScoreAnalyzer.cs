public class ScoreAnalyzer
{
    public const int P_FB = 3;
    public const int P_D = 2;
    public const int P_T = 1;
    public const int I_FB = 2;
    public const int I_D = 1;


    public static UserScore FromActivity(UserActivity act)
    {
        var score = act.PR_fb * P_FB +
                    act.PR_doc * P_D +
                    act.PR_typo * P_T +
                    act.IS_fb * I_FB +
                    act.IS_doc * I_D;

        return new UserScore(act.PR_fb, act.PR_doc, act.PR_typo, act.IS_fb, act.IS_doc, score);
    }
}