using AutoMapper;
using IGB.Application.DTOs;
using IGB.Domain.Entities;
using IGB.Domain.Interfaces;
using IGB.Shared.Common;
using IGB.Shared.DTOs;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace IGB.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository repository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UserService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<UserDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting user {UserId}", id);
            
            var user = await _repository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", id);
                return Result<UserDto>.Failure("User not found");
            }

            var dto = _mapper.Map<UserDto>(user);
            return Result<UserDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return Result<UserDto>.Failure("An error occurred while retrieving the user");
        }
    }

    public async Task<Result<PagedResult<UserDto>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _repository.GetPagedAsync(page, pageSize, cancellationToken);
            var totalCount = await _repository.GetCountAsync(cancellationToken);

            var dtos = _mapper.Map<List<UserDto>>(users);
            var result = new PagedResult<UserDto>
            {
                Items = dtos,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Result<PagedResult<UserDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users list");
            return Result<PagedResult<UserDto>>.Failure("An error occurred while retrieving users");
        }
    }

    public async Task<Result<long>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating new user with email {Email}", dto.Email);

            // Check if email exists
            var existingUser = await _repository.GetByEmailAsync(dto.Email, cancellationToken);
            if (existingUser != null)
            {
                return Result<long>.Failure("Email already exists");
            }

            // Business logic
            var user = _mapper.Map<User>(dto);
            user.CreatedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return Result<long>.Failure("Password is required");
            }

            // Password complexity policy (server-side)
            if (!PasswordMeetsPolicy(dto.Password))
            {
                return Result<long>.Failure("Password must be at least 8 characters and include uppercase, lowercase, number, and special character.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            await _repository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User created successfully with ID {UserId}", user.Id);
            return Result<long>.Success(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return Result<long>.Failure("An error occurred while creating the user");
        }
    }

    public async Task<Result> UpdateAsync(long id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating user {UserId}", id);

            var user = await _repository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return Result.Failure("User not found");
            }

            // Update properties
            _mapper.Map(dto, user);
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} updated successfully", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return Result.Failure("An error occurred while updating the user");
        }
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting user {UserId}", id);

            var user = await _repository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return Result.Failure("User not found");
            }

            // Soft delete
            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} deleted successfully", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return Result.Failure("An error occurred while deleting the user");
        }
    }

    private static bool PasswordMeetsPolicy(string password)
    {
        if (password.Length < 8) return false;
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));
        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}

