static const uint MAX_RIGIDBODIES_NUM = 32;
static const int MAX_SPATIAL_LOOKUP_RENDER_ITERATIONS = 5;

// --- Thread Nums ---

static const uint TN_PS = 512; // Particle Simulation
static const uint TN_PS2 = 512; // Particle Simulation
static const uint TN_R = 32; // Renderer
static const uint TN_RBS1 = 64; // Rigid Body Simulation
static const uint TN_RBS2 = 32; // Rigid Body Simulation
static const uint TN_RBS3 = 512; // Rigid Body Simulation
static const uint TN_S = 512; // Sorter

static const float CENTROID_RADIUS = 2.0;
static const float CENTROID_RADIUS_SQR = CENTROID_RADIUS*CENTROID_RADIUS;
static const float4 COL_RED = float4(1, 0, 0, 1);
static const float RED_TINT_FACTOR = 0.00002;

// --- Float-Int storage precision values ---

// A higher value may cause the half precision to be insufficient, leading to incorrect calculations
static const float INT_FLOAT_PRECISION_RB = 50000.0;
static const float INT_FLOAT_PRECISION_P = 5000.0;

// --- New path flag values ---

static const float PATH_FLAG_OFFSET = 100000.0;
static const float PATH_FLAG_THRESHOLD = PATH_FLAG_OFFSET / 2.0;