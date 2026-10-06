// AmgclBridge: solves 3D linear elasticity systems for GcodeFem with AMGCL.
//
// The stock AMGCL C API (amgcl/lib/amgcl.h) cannot pass rigid-body modes or a block size,
// and those are what make smoothed aggregation converge fast on elasticity. This bridge
// sets them up: CG on 3x3 blocks (double), preconditioned by smoothed-aggregation AMG with the
// 6 rigid-body modes as near null-space. The smoother is selectable, and so is the precision of
// the preconditioner: float blocks halve its memory, but with ILU(0) CG then stalls on thin
// printed structures in bending, so ILU(0) on double blocks (relaxation 4) is the default.
// See AMGCL's tutorial/5.Nullspace/nullspace_block.cpp.

#include <algorithm>
#include <chrono>
#include <cstring>
#include <exception>
#include <vector>

#include <amgcl/adapter/block_matrix.hpp>
#include <amgcl/adapter/crs_tuple.hpp>
#include <amgcl/amg.hpp>
#include <amgcl/backend/builtin.hpp>
#include <amgcl/coarsening/as_scalar.hpp>
#include <amgcl/coarsening/rigid_body_modes.hpp>
#include <amgcl/coarsening/smoothed_aggregation.hpp>
#include <amgcl/make_solver.hpp>
#include <amgcl/relaxation/chebyshev.hpp>
#include <amgcl/relaxation/gauss_seidel.hpp>
#include <amgcl/relaxation/ilu0.hpp>
#include <amgcl/relaxation/iluk.hpp>
#include <amgcl/relaxation/spai0.hpp>
#include <amgcl/solver/cg.hpp>
#include <amgcl/value_type/static_matrix.hpp>

#define BRIDGE_API extern "C" __declspec(dllexport)

namespace {

typedef amgcl::static_matrix<double, 3, 3> DBlock;
typedef amgcl::static_matrix<float, 3, 3> FBlock;
typedef amgcl::backend::builtin<DBlock> SBackend;
typedef amgcl::backend::builtin<FBlock> PBackend;

struct Report {
    int iterations;
    double residual, setup_seconds, solve_seconds;
};

double seconds_since(std::chrono::steady_clock::time_point start) {
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();
}

void copy_message(const char* message, char* buffer, int capacity) {
    if (!buffer || capacity <= 0) return;
    std::strncpy(buffer, message, static_cast<size_t>(capacity) - 1);
    buffer[capacity - 1] = '\0';
}

template <class PrecondBackend, template <class> class Relaxation>
Report solve_with(int n, const int* ptr, const int* col, const double* val,
                  const double* coords, const double* rhs, double* x,
                  double tolerance, int max_iterations)
{
    typedef amgcl::make_solver<
        amgcl::amg<
            PrecondBackend,
            amgcl::coarsening::as_scalar<amgcl::coarsening::smoothed_aggregation>::type,
            Relaxation>,
        amgcl::solver::cg<SBackend>>
        Solver;

    typename Solver::params prm;
    prm.solver.tol = tolerance;
    prm.solver.maxiter = max_iterations;
    prm.precond.coarsening.aggr.eps_strong = 0;

    std::vector<double> coo(coords, coords + n);
    prm.precond.coarsening.nullspace.cols =
        amgcl::coarsening::rigid_body_modes(3, coo, prm.precond.coarsening.nullspace.B);

    const ptrdiff_t rows = n;
    auto A = std::make_tuple(
        rows,
        amgcl::make_iterator_range(ptr, ptr + n + 1),
        amgcl::make_iterator_range(col, col + ptr[n]),
        amgcl::make_iterator_range(val, val + ptr[n]));
    auto Ab = amgcl::adapter::block_matrix<DBlock>(A);

    Report report{};
    auto setup_start = std::chrono::steady_clock::now();
    Solver solve(Ab, prm);
    report.setup_seconds = seconds_since(setup_start);

    std::vector<double> b(rhs, rhs + n);
    std::vector<double> u(x, x + n);
    auto B = amgcl::backend::reinterpret_as_rhs<DBlock>(b);
    auto U = amgcl::backend::reinterpret_as_rhs<DBlock>(u);

    auto solve_start = std::chrono::steady_clock::now();
    std::tie(report.iterations, report.residual) = solve(Ab, B, U);
    report.solve_seconds = seconds_since(solve_start);

    std::copy(u.begin(), u.end(), x);
    return report;
}

} // namespace

BRIDGE_API int amgcl_bridge_version() { return 3; }

// Solves A x = b for a symmetric positive definite elasticity matrix.
//   n       number of scalar unknowns, 3 per node, ordered (ux, uy, uz) per node
//   ptr/col/val  CSR matrix, n rows, 0-based column indices
//   coords  node coordinates, 3 per node (n values in total), for the rigid-body modes
//   x       initial guess on input, solution on output
//   tolerance  relative residual |b - Ax| / |b| to stop at
//   relaxation the AMG smoother: 0 = ILU(0), 1 = SPAI-0, 2 = Chebyshev, 3 = Gauss-Seidel, 5 = ILU(1),
//              all on a single-precision preconditioner; 4 = ILU(0) on a double-precision one
// Returns 0 on success; on failure 1 and a message in error (never throws across the ABI).
BRIDGE_API int amgcl_bridge_solve_elasticity(
    int n, const int* ptr, const int* col, const double* val,
    const double* coords, const double* rhs, double* x,
    double tolerance, int max_iterations, int relaxation,
    int* iterations, double* residual, double* setup_seconds, double* solve_seconds,
    char* error, int error_capacity)
{
    try {
        if (n <= 0 || n % 3 != 0) {
            copy_message("n must be a positive multiple of 3", error, error_capacity);
            return 1;
        }

        Report report;
        switch (relaxation) {
            case 0: report = solve_with<PBackend, amgcl::relaxation::ilu0>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            case 1: report = solve_with<PBackend, amgcl::relaxation::spai0>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            case 2: report = solve_with<PBackend, amgcl::relaxation::chebyshev>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            case 3: report = solve_with<PBackend, amgcl::relaxation::gauss_seidel>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            case 4: report = solve_with<SBackend, amgcl::relaxation::ilu0>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            case 5: report = solve_with<PBackend, amgcl::relaxation::iluk>(n, ptr, col, val, coords, rhs, x, tolerance, max_iterations); break;
            default:
                copy_message("relaxation must be 0..5", error, error_capacity);
                return 1;
        }

        *iterations = report.iterations;
        *residual = report.residual;
        *setup_seconds = report.setup_seconds;
        *solve_seconds = report.solve_seconds;
        return 0;
    } catch (const std::exception& ex) {
        copy_message(ex.what(), error, error_capacity);
        return 1;
    } catch (...) {
        copy_message("unknown native exception", error, error_capacity);
        return 1;
    }
}
