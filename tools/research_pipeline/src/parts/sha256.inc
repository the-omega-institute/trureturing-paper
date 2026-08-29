static int g_lock_fd = -1;
static volatile sig_atomic_t g_stop = 0;

typedef struct {
    uint8_t data[64];
    uint32_t datalen;
    uint64_t bitlen;
    uint32_t state[8];
} sha256_ctx;

static const uint32_t k256[64] = {
    0x428a2f98U, 0x71374491U, 0xb5c0fbcfU, 0xe9b5dba5U, 0x3956c25bU, 0x59f111f1U, 0x923f82a4U, 0xab1c5ed5U,
    0xd807aa98U, 0x12835b01U, 0x243185beU, 0x550c7dc3U, 0x72be5d74U, 0x80deb1feU, 0x9bdc06a7U, 0xc19bf174U,
    0xe49b69c1U, 0xefbe4786U, 0x0fc19dc6U, 0x240ca1ccU, 0x2de92c6fU, 0x4a7484aaU, 0x5cb0a9dcU, 0x76f988daU,
    0x983e5152U, 0xa831c66dU, 0xb00327c8U, 0xbf597fc7U, 0xc6e00bf3U, 0xd5a79147U, 0x06ca6351U, 0x14292967U,
    0x27b70a85U, 0x2e1b2138U, 0x4d2c6dfcU, 0x53380d13U, 0x650a7354U, 0x766a0abbU, 0x81c2c92eU, 0x92722c85U,
    0xa2bfe8a1U, 0xa81a664bU, 0xc24b8b70U, 0xc76c51a3U, 0xd192e819U, 0xd6990624U, 0xf40e3585U, 0x106aa070U,
    0x19a4c116U, 0x1e376c08U, 0x2748774cU, 0x34b0bcb5U, 0x391c0cb3U, 0x4ed8aa4aU, 0x5b9cca4fU, 0x682e6ff3U,
    0x748f82eeU, 0x78a5636fU, 0x84c87814U, 0x8cc70208U, 0x90befffaU, 0xa4506cebU, 0xbef9a3f7U, 0xc67178f2U
};

static uint32_t rotr32(uint32_t x, uint32_t n) { return (x >> n) | (x << (32U - n)); }
static uint32_t ch32(uint32_t x, uint32_t y, uint32_t z) { return (x & y) ^ (~x & z); }
static uint32_t maj32(uint32_t x, uint32_t y, uint32_t z) { return (x & y) ^ (x & z) ^ (y & z); }
static uint32_t ep0(uint32_t x) { return rotr32(x, 2U) ^ rotr32(x, 13U) ^ rotr32(x, 22U); }
static uint32_t ep1(uint32_t x) { return rotr32(x, 6U) ^ rotr32(x, 11U) ^ rotr32(x, 25U); }
static uint32_t sig0(uint32_t x) { return rotr32(x, 7U) ^ rotr32(x, 18U) ^ (x >> 3U); }
static uint32_t sig1(uint32_t x) { return rotr32(x, 17U) ^ rotr32(x, 19U) ^ (x >> 10U); }

static void sha256_transform(sha256_ctx *ctx, const uint8_t data[]) {
    uint32_t a, b, c, d, e, f, g, h, i, j, t1, t2, m[64];
    for (i = 0, j = 0; i < 16; ++i, j += 4) {
        m[i] = ((uint32_t)data[j] << 24U) | ((uint32_t)data[j + 1] << 16U) |
               ((uint32_t)data[j + 2] << 8U) | (uint32_t)data[j + 3];
    }
    for (; i < 64; ++i) m[i] = sig1(m[i - 2]) + m[i - 7] + sig0(m[i - 15]) + m[i - 16];
    a = ctx->state[0]; b = ctx->state[1]; c = ctx->state[2]; d = ctx->state[3];
    e = ctx->state[4]; f = ctx->state[5]; g = ctx->state[6]; h = ctx->state[7];
    for (i = 0; i < 64; ++i) {
        t1 = h + ep1(e) + ch32(e, f, g) + k256[i] + m[i];
        t2 = ep0(a) + maj32(a, b, c);
        h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
    }
    ctx->state[0] += a; ctx->state[1] += b; ctx->state[2] += c; ctx->state[3] += d;
    ctx->state[4] += e; ctx->state[5] += f; ctx->state[6] += g; ctx->state[7] += h;
}

static void sha256_init(sha256_ctx *ctx) {
    ctx->datalen = 0;
    ctx->bitlen = 0;
    ctx->state[0] = 0x6a09e667U; ctx->state[1] = 0xbb67ae85U;
    ctx->state[2] = 0x3c6ef372U; ctx->state[3] = 0xa54ff53aU;
    ctx->state[4] = 0x510e527fU; ctx->state[5] = 0x9b05688cU;
    ctx->state[6] = 0x1f83d9abU; ctx->state[7] = 0x5be0cd19U;
}

static void sha256_update(sha256_ctx *ctx, const uint8_t data[], size_t len) {
    for (size_t i = 0; i < len; ++i) {
        ctx->data[ctx->datalen++] = data[i];
        if (ctx->datalen == 64U) {
            sha256_transform(ctx, ctx->data);
            ctx->bitlen += 512U;
            ctx->datalen = 0;
        }
    }
}

static void sha256_final(sha256_ctx *ctx, uint8_t hash[]) {
    uint32_t i = ctx->datalen;
    ctx->data[i++] = 0x80U;
    if (i > 56U) {
        while (i < 64U) ctx->data[i++] = 0;
        sha256_transform(ctx, ctx->data);
        i = 0;
    }
    while (i < 56U) ctx->data[i++] = 0;
    ctx->bitlen += (uint64_t)ctx->datalen * 8U;
    ctx->data[63] = (uint8_t)ctx->bitlen;
    ctx->data[62] = (uint8_t)(ctx->bitlen >> 8U);
    ctx->data[61] = (uint8_t)(ctx->bitlen >> 16U);
    ctx->data[60] = (uint8_t)(ctx->bitlen >> 24U);
    ctx->data[59] = (uint8_t)(ctx->bitlen >> 32U);
    ctx->data[58] = (uint8_t)(ctx->bitlen >> 40U);
    ctx->data[57] = (uint8_t)(ctx->bitlen >> 48U);
    ctx->data[56] = (uint8_t)(ctx->bitlen >> 56U);
    sha256_transform(ctx, ctx->data);
    for (i = 0; i < 4U; ++i) {
        hash[i]      = (uint8_t)((ctx->state[0] >> (24U - i * 8U)) & 0xffU);
        hash[i + 4]  = (uint8_t)((ctx->state[1] >> (24U - i * 8U)) & 0xffU);
        hash[i + 8]  = (uint8_t)((ctx->state[2] >> (24U - i * 8U)) & 0xffU);
        hash[i + 12] = (uint8_t)((ctx->state[3] >> (24U - i * 8U)) & 0xffU);
        hash[i + 16] = (uint8_t)((ctx->state[4] >> (24U - i * 8U)) & 0xffU);
        hash[i + 20] = (uint8_t)((ctx->state[5] >> (24U - i * 8U)) & 0xffU);
        hash[i + 24] = (uint8_t)((ctx->state[6] >> (24U - i * 8U)) & 0xffU);
        hash[i + 28] = (uint8_t)((ctx->state[7] >> (24U - i * 8U)) & 0xffU);
    }
}
