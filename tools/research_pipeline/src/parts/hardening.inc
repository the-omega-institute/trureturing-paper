static int gate_receipts(int argc, char **argv) {
    if (argc < 1) {
        fprintf(stderr, "gate receipts requires at least one JSON receipt\n");
        return 2;
    }
    for (int i = 0; i < argc; ++i) {
        if (!regular_nonempty(argv[i]) || !file_contains_token(argv[i], "\"status\":\"pass\"")) {
            fprintf(stderr, "receipt is missing or does not report pass: %s\n", argv[i]);
            return 1;
        }
    }
    printf("receipt gate: pass (%d receipts)\n", argc);
    return 0;
}

static int gate_clean_text(int argc, char **argv) {
    if (argc < 1) {
        fprintf(stderr, "gate clean-text requires at least one text file\n");
        return 2;
    }
    const char *markers[] = {
        "\\placeholder{", "[Paper title", "[Author names", "REQUIRED", "TODO", "TBD", "FIXME",
        "State the problem", "Motivate the mathematical problem", "Organize prior work",
        "Define objects", "State each result", "Explain the proof idea", "Describe the Lean environment",
        "Show failed stronger statements", "Explain mathematical consequences", "Give the repository revision",
        "Retain this sentence only after verification"
    };
    for (int i = 0; i < argc; ++i) {
        if (!regular_nonempty(argv[i])) {
            fprintf(stderr, "text file missing or empty: %s\n", argv[i]);
            return 1;
        }
        for (size_t j = 0; j < ARRAY_LEN(markers); ++j) {
            if (file_contains_token(argv[i], markers[j])) {
                fprintf(stderr, "unresolved marker '%s' in %s\n", markers[j], argv[i]);
                return 1;
            }
        }
    }
    printf("clean-text gate: pass (%d files)\n", argc);
    return 0;
}
