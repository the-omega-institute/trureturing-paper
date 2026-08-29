static void print_status(const char *config_path) {
    fkst_config cfg;
    fkst_state st;
    load_config(config_path, &cfg);
    load_state(&cfg, &st);
    printf("root=%s\ncurrent=%s\ntransitions=%u\nlast_stage=%s\nlast_outcome=%s\nlast_artifact_hash=%s\n",
           cfg.root, st.current, st.transitions, st.last_stage, st.last_outcome, st.last_artifact_hash);
}

static void print_graph(const char *config_path) {
    fkst_config cfg;
    load_config(config_path, &cfg);
    puts("digraph fkst_research_pipeline {");
    puts("  rankdir=LR;");
    for (size_t i = 0; i < cfg.stage_count; ++i) {
        const fkst_stage *s = &cfg.stages[i];
        printf("  \"%s\" -> \"%s\" [label=\"pass\"];\n", s->id, s->success_next);
        printf("  \"%s\" -> \"%s\" [label=\"fail\"];\n", s->id, s->failure_next);
    }
    puts("}");
}

static void usage(FILE *fp) {
    fputs(
        "Usage:\n"
        "  fkst-pipeline run --config FILE [--from STAGE] [--dry-run]\n"
        "  fkst-pipeline status --config FILE\n"
        "  fkst-pipeline graph --config FILE\n"
        "  fkst-pipeline gate novelty LEDGER.tsv\n"
        "  fkst-pipeline gate claims ROOT CLAIMS.tsv\n"
        "  fkst-pipeline gate journals JOURNALS.tsv\n"
        "  fkst-pipeline gate reviews ROUND1.tsv ROUND2.tsv ROUND3.tsv\n"
        "  fkst-pipeline gate receipts RECEIPT.json [RECEIPT.json ...]\n"
        "  fkst-pipeline gate clean-text FILE [FILE ...]\n"
        "  fkst-pipeline gate pdf FILE.pdf\n", fp);
}

int main(int argc, char **argv) {
    if (argc < 2) { usage(stderr); return 2; }
    if (strcmp(argv[1], "gate") == 0) {
        if (argc < 4) { usage(stderr); return 2; }
        if (strcmp(argv[2], "novelty") == 0 && argc == 4) return gate_novelty(argv[3]);
        if (strcmp(argv[2], "claims") == 0 && argc == 5) return gate_claims(argv[3], argv[4]);
        if (strcmp(argv[2], "journals") == 0 && argc == 4) return gate_journals(argv[3]);
        if (strcmp(argv[2], "reviews") == 0) return gate_reviews(argc - 3, argv + 3);
        if (strcmp(argv[2], "receipts") == 0) return gate_receipts(argc - 3, argv + 3);
        if (strcmp(argv[2], "clean-text") == 0) return gate_clean_text(argc - 3, argv + 3);
        if (strcmp(argv[2], "pdf") == 0 && argc == 4) return gate_pdf(argv[3]);
        usage(stderr); return 2;
    }
    const char *config = NULL, *from = NULL;
    bool dry = false;
    for (int i = 2; i < argc; ++i) {
        if (strcmp(argv[i], "--config") == 0 && i + 1 < argc) config = argv[++i];
        else if (strcmp(argv[i], "--from") == 0 && i + 1 < argc) from = argv[++i];
        else if (strcmp(argv[i], "--dry-run") == 0) dry = true;
        else { usage(stderr); return 2; }
    }
    if (!config) { fprintf(stderr, "--config is required\n"); return 2; }
    if (strcmp(argv[1], "run") == 0) {
        if (dry) plan_pipeline(argv[0], config, from);
        else run_pipeline(argv[0], config, from);
        return 0;
    }
    if (strcmp(argv[1], "status") == 0) { print_status(config); return 0; }
    if (strcmp(argv[1], "graph") == 0) { print_graph(config); return 0; }
    usage(stderr);
    return 2;
}
