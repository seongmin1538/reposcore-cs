public class ScoreAnalyzer
{
    public const int P_FB = 3;
    public const int P_D = 2;
    public const int P_T = 1;
    public const int I_FB = 2;
    public const int I_D = 1;


    public static UserScore FromActivity(UserActivity act)
    {
        var (p_fb, p_d, p_t, i_fb, i_d) = act;

        var p_valid = getP_valid(p_fb, p_d, p_t);
        var i_valid = getI_valid(i_fb, i_d, p_valid);

        var p_star_fb = Math.Min(p_fb, p_valid);
        var p_star_d = Math.Min(p_d, p_valid - p_fb);
        var p_star_t = p_valid - p_star_fb - p_star_d;

        var i_star_fb = Math.Min(i_fb, i_valid);
        var i_star_d = i_valid - i_star_fb;

        var score = p_star_fb * P_FB +
                    p_star_d * P_D +
                    p_star_t * P_T +
                    i_star_fb * I_FB +
                    i_star_d * I_D;

        return new UserScore(act.PR_fb, act.PR_doc, act.PR_typo, act.IS_fb, act.IS_doc, score);
    }

    private static int getP_valid(int p_fb, int p_d, int p_t)
    {
        return p_fb + Math.Min(p_d + p_t, 3 * Math.Max(p_fb, 1));
    }

    private static int getI_valid(int i_fb, int i_d, int p_valid)
    {
        return Math.Min(i_fb + i_d, 4 * p_valid);
    }
    
}